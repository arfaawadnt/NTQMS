using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.EnvironmentOfCare;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.EnvironmentOfCare;

/// <summary>
/// The M15 dashboard: round completion, the open-findings backlog (with the critical subset), and
/// drill coverage and mean effectiveness score, rolled up across both aggregates.
/// </summary>
public class EocSummaryTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Conductor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 9, 1);

    private static AppDbContext NewContext()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"eoc-{Guid.NewGuid()}")
                .AddInterceptors(new TenantStampInterceptor(tenant))
                .Options, tenant);
    }

    [Fact]
    public async Task Summary_rolls_up_rounds_findings_and_drills()
    {
        var db = NewContext();

        // Round 1: completed with one critical (open) and one resolved finding.
        var r1 = SafetyRound.Schedule("EOR-1", "ICU", RoundType.FireSafety, Date);
        r1.Start(Conductor);
        r1.AddFinding("Blocked exit", FindingSeverity.Critical);
        var resolvedId = r1.AddFinding("Loose cable", FindingSeverity.Low);
        r1.ResolveFinding(resolvedId, "Secured.", Now);
        r1.Complete();
        r1.TenantId = TenantId;

        // Round 2: still scheduled, no findings.
        var r2 = SafetyRound.Schedule("EOR-2", "Ward B", RoundType.GeneralSafety, Date);
        r2.TenantId = TenantId;
        db.SafetyRounds.AddRange(r1, r2);

        // Two drills: one evaluated (score 80), one scheduled.
        var d1 = Drill.Schedule("EOD-1", DrillType.Fire, "Tower A", Date);
        d1.Execute(Now, 30);
        d1.Evaluate(80, "notes");
        d1.TenantId = TenantId;
        var d2 = Drill.Schedule("EOD-2", DrillType.CodeBlue, "ED", Date);
        d2.TenantId = TenantId;
        db.Drills.AddRange(d1, d2);
        await db.SaveChangesAsync();

        var s = await new GetEocSummaryHandler(db).Handle(new GetEocSummaryQuery(), CancellationToken.None);

        s.RoundsScheduled.Should().Be(1);
        s.RoundsCompleted.Should().Be(1);
        s.OpenFindings.Should().Be(1, "the low finding was resolved");
        s.CriticalOpenFindings.Should().Be(1);
        s.DrillsScheduled.Should().Be(1);
        s.DrillsEvaluated.Should().Be(1);
        s.MeanDrillScore.Should().Be(80m);
    }
}
