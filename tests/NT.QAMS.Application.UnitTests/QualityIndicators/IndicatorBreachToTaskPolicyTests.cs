using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.QualityIndicators;
using NT.QAMS.Domain.QualityIndicators;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.QualityIndicators;

/// <summary>
/// The breach workflow (HQMS M06): an IndicatorBreached event opens exactly one analysis
/// task for the quality function, and at-least-once redelivery does not duplicate it.
/// </summary>
public class IndicatorBreachToTaskPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static (AppDbContext Db, CurrentTenant Tenant) NewContext()
    {
        var tenant = new CurrentTenant();
        var clock = new FixedClock(Now);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ind-breach-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(clock, new FakeCurrentUser()),
                new TenantStampInterceptor(tenant),
                new OutboxInterceptor(clock))
            .Options;
        return (new AppDbContext(options, tenant), tenant);
    }

    private static async Task<(AppDbContext Db, CurrentTenant Tenant, IndicatorBreached Event)> SeedAsync()
    {
        var (db, tenant) = NewContext();
        tenant.Set(TenantId);

        var indicator = QualityIndicator.Define(
            "IND-2026-0001", "FALL-1", "Falls per 1,000 patient-days", null,
            "Falls", "Patient-days", "per 1,000 patient-days", 1000m,
            IndicatorFrequency.Monthly, IndicatorDirection.LowerIsBetter);
        indicator.TenantId = TenantId;
        indicator.SetTargets(2m, 4m, 6m);
        db.QualityIndicators.Add(indicator);
        await db.SaveChangesAsync();

        var period = new DateOnly(2026, 8, 1);
        var evt = new IndicatorBreached(indicator.Id, indicator.IndicatorRef, "FALL-1", period, 7m, 6m);

        tenant.Clear(); // background scope: no ambient tenant until the policy sets it
        return (db, tenant, evt);
    }

    private static IndicatorBreachToTaskPolicy Policy(AppDbContext db, CurrentTenant tenant) =>
        new(db, tenant, new FixedClock(Now), NullLogger<IndicatorBreachToTaskPolicy>.Instance);

    [Fact]
    public async Task Breach_opens_a_single_analysis_task_for_the_quality_manager()
    {
        var (db, tenant, evt) = await SeedAsync();

        await Policy(db, tenant).Handle(new DomainEventNotification<IndicatorBreached>(evt), CancellationToken.None);

        var task = await db.WorkTasks.SingleAsync();
        task.SubjectRef.Should().Be("INDBREACH:FALL-1:2026-08-01");
        task.AssigneeRole.Should().Be("QualityManager");
        task.Status.Should().Be(WorkTaskStatus.Pending);
        task.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task Redelivery_is_idempotent_no_duplicate_task()
    {
        var (db, tenant, evt) = await SeedAsync();
        var policy = Policy(db, tenant);
        var notification = new DomainEventNotification<IndicatorBreached>(evt);

        await policy.Handle(notification, CancellationToken.None);
        await policy.Handle(notification, CancellationToken.None);

        (await db.WorkTasks.CountAsync()).Should().Be(1);
    }
}
