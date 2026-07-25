using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class PrecisionStudyTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static PrecisionStudy New(decimal? repClaim = null, decimal? wlClaim = null) =>
        PrecisionStudy.Configure("PR-2026-0001", "Glucose", "mmol/L", "Level 1 (5.5)", repClaim, wlClaim);

    [Fact]
    public void Anova_separates_within_run_from_between_run_and_totals_them()
    {
        // Run A: 10,12 (mean 11); Run B: 14,16 (mean 15). Grand 13, k=2, n=2.
        // MSW = 2 → repeatability SD √2; between-run var = (16−2)/2 = 7 → SD √7;
        // within-lab var = 9 → SD exactly 3.
        var study = New();
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 14m);
        study.AddMeasurement("B", 16m);

        study.Calculate();

        study.GrandMean.Should().Be(13m);
        study.RepeatabilitySd.Should().BeApproximately(1.4142m, 0.0005m);
        study.BetweenRunSd.Should().BeApproximately(2.6458m, 0.0005m);
        study.WithinLabSd.Should().Be(3m);
        study.WithinLabCvPct.Should().BeApproximately(23.0769m, 0.001m); // 3/13*100
        study.State.Should().Be(PrecisionState.Calculated);
    }

    [Fact]
    public void No_between_run_variance_when_run_means_are_equal()
    {
        // Both runs mean 11; MSB ≤ MSW so the between component floors at 0.
        var study = New();
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 10m);
        study.AddMeasurement("B", 12m);

        study.Calculate();

        study.BetweenRunSd.Should().Be(0m);
        study.WithinLabSd.Should().Be(study.RepeatabilitySd);
    }

    [Fact]
    public void Claims_are_verified_per_component()
    {
        var study = New(repClaim: 15m, wlClaim: 20m);
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 14m);
        study.AddMeasurement("B", 16m);

        study.Calculate();

        // Repeatability CV ≈ 10.88% ≤ 15% passes; within-lab CV ≈ 23.08% > 20% fails.
        study.MeetsRepeatabilityClaim.Should().BeTrue();
        study.MeetsWithinLabClaim.Should().BeFalse();
    }

    [Fact]
    public void Guards_minimums_editing_invalidation_and_sign_off_freeze()
    {
        var study = New();
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        var oneRun = () => study.Calculate();
        oneRun.Should().Throw<DomainException>().Which.Code.Should().Be("PR-010");

        study.AddMeasurement("B", 14m); // single replicate in B
        var singleRep = () => study.Calculate();
        singleRep.Should().Throw<DomainException>().Which.Code.Should().Be("PR-011");

        study.AddMeasurement("B", 16m);
        study.Calculate();
        study.State.Should().Be(PrecisionState.Calculated);

        study.AddMeasurement("C", 20m);
        study.State.Should().Be(PrecisionState.DataEntry); // recompute invalidated
        study.WithinLabSd.Should().BeNull();

        study.AddMeasurement("C", 22m);
        study.Calculate();
        study.SignOff(Qm, Now);
        study.DomainEvents.Should().ContainSingle(e => e is PrecisionStudySignedOff);
        var mutate = () => study.AddMeasurement("D", 1m);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("PR-013");
    }
}
