using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class LinearityStudyTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static LinearityStudy New(decimal adl = 5m) => LinearityStudy.Configure(
        "LIN-2026-0001", "Glucose", "mmol/L", "Hexokinase (Cobas c503)", adl);

    [Fact]
    public void Perfectly_linear_series_verifies_the_full_range_as_amr()
    {
        // Five levels, duplicate measurements, exactly y = 1.02x (2% proportional bias, still linear).
        var study = New();
        foreach (var assigned in new[] { 2m, 5m, 10m, 20m, 30m })
        {
            study.AddMeasurement(assigned, 1.02m * assigned);
            study.AddMeasurement(assigned, 1.02m * assigned);
        }

        study.Calculate();

        study.Slope.Should().Be(1.02m);
        study.Intercept.Should().Be(0m);
        study.CorrelationR.Should().Be(1m);
        study.IsLinear.Should().BeTrue();
        study.AmrLow.Should().Be(2m);
        study.AmrHigh.Should().Be(30m);

        var levels = study.LevelAssessments();
        levels.Should().HaveCount(5).And.OnlyContain(l => l.Passes && l.DeviationPct == 0m);
        levels[0].RecoveryPct.Should().Be(102m);
    }

    [Fact]
    public void Hooked_top_level_fails_and_the_amr_shrinks_to_the_passing_span()
    {
        // Linear through 20, then the 30 level reads 24 (severe high-end hook).
        var study = New(adl: 5m);
        foreach (var assigned in new[] { 2m, 5m, 10m, 20m })
        {
            study.AddMeasurement(assigned, assigned);
        }

        study.AddMeasurement(30m, 24m);

        study.Calculate();

        study.IsLinear.Should().BeFalse();
        var levels = study.LevelAssessments();
        levels.Single(l => l.AssignedValue == 30m).Passes.Should().BeFalse();
        // The verified range is the contiguous passing low-end span.
        study.AmrLow.Should().Be(2m);
        study.AmrHigh.Should().Be(20m);
    }

    [Fact]
    public void Editing_measurements_invalidates_a_prior_calculation()
    {
        var study = New();
        foreach (var assigned in new[] { 1m, 2m, 3m, 4m })
        {
            study.AddMeasurement(assigned, assigned);
        }

        study.Calculate();
        study.State.Should().Be(LinearityState.Calculated);

        study.AddMeasurement(5m, 5m);
        study.State.Should().Be(LinearityState.DataEntry);
        study.Slope.Should().BeNull();
        study.LevelAssessments().Should().BeEmpty();
    }

    [Fact]
    public void Guards_minimum_levels_config_bounds_and_sign_off_freeze()
    {
        var badAdl = () => LinearityStudy.Configure("LIN-1", "A", "u", "M", 0m);
        badAdl.Should().Throw<DomainException>().Which.Code.Should().Be("LIN-002");

        var study = New();
        study.AddMeasurement(1m, 1m);
        study.AddMeasurement(2m, 2m);
        study.AddMeasurement(3m, 3m);
        var tooFew = () => study.Calculate();
        tooFew.Should().Throw<DomainException>().Which.Code.Should().Be("LIN-010");

        study.AddMeasurement(4m, 4m);
        study.Calculate();
        study.SignOff(Qm, Now);
        study.DomainEvents.Should().ContainSingle(e => e is LinearityStudySignedOff);

        var mutate = () => study.AddMeasurement(5m, 5m);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("LIN-013");
    }
}
