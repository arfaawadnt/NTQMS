using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class QcProfileTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

    private static QcProfile New() => QcProfile.Create("Glucose", "Analyzer-1", "Lot-A", 5.5m, 0.2m);

    [Fact]
    public void Update_targets_requires_a_reason()
    {
        var p = New();
        var act = () => p.UpdateTargets(5.6m, 0.25m, "  ", T0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("QC-012");
    }

    [Fact]
    public void Update_targets_records_reason_and_effective_date()
    {
        var p = New();
        p.UpdateTargets(5.6m, 0.25m, "New control lot mean per certificate", T0);

        p.TargetMean.Should().Be(5.6m);
        p.TargetSd.Should().Be(0.25m);
        p.LastTargetChangeReason.Should().Be("New control lot mean per certificate");
        p.TargetEffectiveFromUtc.Should().Be(T0);
    }

    [Fact]
    public void Target_changes_are_forward_only()
    {
        var p = New();
        p.UpdateTargets(5.6m, 0.25m, "first change", T0);

        var act = () => p.UpdateTargets(5.7m, 0.26m, "backdated", T0.AddDays(-1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("QC-013");
    }

    [Fact]
    public void Target_sd_must_stay_positive()
    {
        var p = New();
        var act = () => p.UpdateTargets(5.6m, 0m, "reason", T0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("QC-002");
    }
}
