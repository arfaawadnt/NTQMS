using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Sla;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Infrastructure.Jobs;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Abstractions;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Operations;

/// <summary>
/// Proves the escalation lifecycle end-to-end: arm on CAPA planned, cancel on
/// completion, and the sweep tick advancing an overdue timer into a work-task.
/// </summary>
public class EscalationFlowTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Owner = Guid.CreateVersion7();

    private static ServiceProvider BuildProvider(string dbName, DateTimeOffset now)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock(now));
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentTenantSetter>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddScoped<AuditStampInterceptor>();
        services.AddScoped<TenantStampInterceptor>();
        services.AddScoped<OutboxInterceptor>();
        services.AddDbContext<AppDbContext>((sp, o) => o
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(
                sp.GetRequiredService<AuditStampInterceptor>(),
                sp.GetRequiredService<TenantStampInterceptor>(),
                sp.GetRequiredService<OutboxInterceptor>()));
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedNcWithCapaAsync(ServiceProvider provider, DateOnly due)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<CurrentTenant>();
        tenant.Set(TenantId);

        var nc = Nonconformance.Raise("NC-2026-0001", "T", "D", 3, 3, NcSourceType.Internal, Guid.CreateVersion7());
        nc.Submit();
        nc.Triage(Guid.CreateVersion7());
        nc.RecordRca(RcaMethod.FiveWhys, "cause", Guid.CreateVersion7());
        var actionId = nc.PlanCapaAction(CapaActionType.Corrective, "fix", Owner, due);
        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync();
        return actionId;
    }

    [Fact]
    public async Task Capa_planned_arms_timer_then_completion_cancels_it()
    {
        var provider = BuildProvider($"esc-{Guid.NewGuid()}", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var actionId = await SeedNcWithCapaAsync(provider, new DateOnly(2026, 6, 15));
        var subjectRef = $"CAPA:{actionId:N}";

        // Arm policy.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var setter = scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>();
            var planned = new CapaActionPlanned(
                (await db.Nonconformances.IgnoreQueryFilters().SingleAsync()).Id,
                "NC-2026-0001", actionId, Owner, new DateOnly(2026, 6, 15));
            await new ArmEscalationOnCapaPlannedPolicy(db, setter)
                .Handle(new DomainEventNotification<CapaActionPlanned>(planned), CancellationToken.None);
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var timer = await db.EscalationTimers.IgnoreQueryFilters().SingleAsync(t => t.SubjectRef == subjectRef);
            timer.Active.Should().BeTrue();
            timer.OwnerUserId.Should().Be(Owner);
        }

        // Cancel policy on completion.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var setter = scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>();
            var ncId = (await db.Nonconformances.IgnoreQueryFilters().SingleAsync()).Id;
            await new CancelEscalationOnCapaCompletedPolicy(db, setter).Handle(
                new DomainEventNotification<CapaActionCompleted>(
                    new CapaActionCompleted(ncId, "NC-2026-0001", actionId)), CancellationToken.None);
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.EscalationTimers.IgnoreQueryFilters().SingleAsync()).Active.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Sweep_tick_advances_overdue_timer_and_emits_escalation_event()
    {
        // "Now" is well past a deadline of 2026-06-15 end-of-day + 24h.
        var provider = BuildProvider($"tick-{Guid.NewGuid()}", new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = scope.ServiceProvider.GetRequiredService<CurrentTenant>();
            tenant.Set(TenantId);
            var deadline = new DateTimeOffset(2026, 6, 15, 23, 59, 59, TimeSpan.Zero);
            var timer = EscalationTimer.Arm("CAPA:xyz", Owner, deadline);
            timer.TenantId = TenantId;
            db.EscalationTimers.Add(timer);
            await db.SaveChangesAsync();
            db.Set<Infrastructure.Persistence.Outbox.OutboxEvent>()
                .RemoveRange(await db.Set<Infrastructure.Persistence.Outbox.OutboxEvent>().ToListAsync());
            await db.SaveChangesAsync();
        }

        var sweep = new ScheduledSweepService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IClock>(),
            NullLogger<ScheduledSweepService>.Instance);
        await sweep.RunSweepAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var timer = await db.EscalationTimers.IgnoreQueryFilters().SingleAsync();
            timer.Level.Should().Be(1, "the deadline+24h step is due at the 2026-06-20 tick");

            var outbox = await db.Set<Infrastructure.Persistence.Outbox.OutboxEvent>().ToListAsync();
            outbox.Should().Contain(e =>
                e.EventType.Contains(nameof(EscalationTriggered)) && e.TenantId == TenantId);
        }
    }
}
