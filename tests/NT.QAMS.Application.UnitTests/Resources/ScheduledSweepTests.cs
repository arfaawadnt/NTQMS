using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Infrastructure.Jobs;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Abstractions;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Resources;

/// <summary>
/// End-to-end sweep test on the real service + DbContext + interceptors:
/// cross-tenant discovery, guarded transitions, outbox events, idempotent re-run.
/// </summary>
public class ScheduledSweepTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 7, 22);
    private static readonly Guid TenantA = Guid.CreateVersion7();
    private static readonly Guid TenantB = Guid.CreateVersion7();
    private static readonly Guid Trainee = Guid.CreateVersion7();
    private static readonly Guid Assessor = Guid.CreateVersion7();

    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock(Now));
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddScoped<AuditStampInterceptor>();
        services.AddScoped<TenantStampInterceptor>();
        services.AddScoped<OutboxInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(
                sp.GetRequiredService<AuditStampInterceptor>(),
                sp.GetRequiredService<TenantStampInterceptor>(),
                sp.GetRequiredService<OutboxInterceptor>()));
        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<CurrentTenant>();

        tenant.Set(TenantA);
        var overdue = EquipmentItem.Register("EQP-2026-0001", "Balance", "SN-1", null, 30, 7);
        overdue.LogCalibration(Today.AddDays(-60), "Metrology", "Pass", null); // due -30, grace gone -23
        var current = EquipmentItem.Register("EQP-2026-0002", "Centrifuge", "SN-2", null, 365, 14);
        current.LogCalibration(Today.AddDays(-10), "Metrology", "Pass", null); // not due
        db.EquipmentItems.AddRange(overdue, current);
        await db.SaveChangesAsync();

        tenant.Set(TenantB);
        var comp = CompetencyRecord.Assign(Trainee, "SOP-XYZ", null, 12);
        comp.ScoreAssessment(90, Assessor, Now.AddMonths(-13));
        comp.Authorize(Assessor, Today.AddMonths(-13)); // expired a month ago
        db.Competencies.Add(comp);
        await db.SaveChangesAsync();

        // Clear the seed-time outbox rows so assertions see only sweep output.
        db.Set<OutboxEvent>().RemoveRange(await db.Set<OutboxEvent>().ToListAsync());
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Sweep_transitions_due_lockout_and_expiry_across_tenants_and_is_idempotent()
    {
        var provider = BuildProvider($"sweep-{Guid.NewGuid()}");
        await SeedAsync(provider);

        var sweep = new ScheduledSweepService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IClock>(),
            NullLogger<ScheduledSweepService>.Instance);

        var (due, locked, expired, suspended) = await sweep.RunSweepAsync(CancellationToken.None);

        due.Should().Be(1, "only the overdue item transitions");
        locked.Should().Be(0, "lockout happens on a later sweep after NeedsCalibration");
        expired.Should().Be(1);
        suspended.Should().Be(0);

        // Second run: the overdue item (now NeedsCalibration, grace long gone) locks out.
        var second = await sweep.RunSweepAsync(CancellationToken.None);
        second.Locked.Should().Be(1);

        // Third run: nothing left to do — idempotent.
        var third = await sweep.RunSweepAsync(CancellationToken.None);
        third.Should().Be((0, 0, 0, 0));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var items = await db.EquipmentItems.IgnoreQueryFilters().ToListAsync();
        items.Single(e => e.SerialNumber == "SN-1").Status.Should().Be(EquipmentStatus.OutOfService);
        items.Single(e => e.SerialNumber == "SN-2").Status.Should().Be(EquipmentStatus.Active);

        var comp = await db.Competencies.IgnoreQueryFilters().SingleAsync();
        comp.Status.Should().Be(CompetencyStatus.PendingTraining);

        // Events flowed to the outbox with the right tenants.
        var outbox = await db.Set<OutboxEvent>().ToListAsync();
        outbox.Should().Contain(e => e.EventType.Contains(nameof(CalibrationDue)) && e.TenantId == TenantA);
        outbox.Should().Contain(e => e.EventType.Contains(nameof(EquipmentLockedOut)) && e.TenantId == TenantA);
        outbox.Should().Contain(e => e.EventType.Contains(nameof(CompetencyExpired)) && e.TenantId == TenantB);
    }
}
