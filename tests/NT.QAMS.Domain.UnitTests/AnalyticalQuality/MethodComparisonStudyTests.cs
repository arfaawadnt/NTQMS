using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class MethodComparisonStudyTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static MethodComparisonStudy New() => MethodComparisonStudy.Configure(
        "MC-2026-0001", "Glucose", "mmol/L", "Reference analyzer", "Candidate analyzer");

    [Fact]
    public void Perfectly_linear_data_recovers_the_exact_slope_and_intercept_for_both_regressions()
    {
        // y = 2x + 1 exactly: Deming and Passing–Bablok must both recover it,
        // correlation is 1, and the mean difference equals mean(x)+1.
        var study = New();
        foreach (var xi in new[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m })
        {
            study.AddPair(xi, 2m * xi + 1m, $"S{xi}");
        }

        study.Calculate();

        study.PearsonR.Should().Be(1m);
        study.DemingSlope.Should().Be(2m);
        study.DemingIntercept.Should().Be(1m);
        study.PassingBablokSlope.Should().Be(2m);
        study.PassingBablokIntercept.Should().Be(1m);
        study.State.Should().Be(MethodComparisonState.Calculated);
    }

    [Fact]
    public void Bland_altman_computes_the_mean_bias_and_ninety_five_percent_limits()
    {
        // Differences (y − x): +2, +4, +0, +2, +2  → mean 2.0, sample SD √2 ≈ 1.4142.
        var study = New();
        study.AddPair(10m, 12m, null); // +2
        study.AddPair(20m, 24m, null); // +4
        study.AddPair(30m, 30m, null); // 0
        study.AddPair(40m, 42m, null); // +2
        study.AddPair(50m, 52m, null); // +2

        study.Calculate();

        study.MeanBias.Should().Be(2m);
        study.BiasSd.Should().BeApproximately(1.4142m, 0.0005m);
        // 2 ± 1.96·1.4142 = 2 ± 2.7719
        study.LimitOfAgreementLower.Should().BeApproximately(-0.7719m, 0.001m);
        study.LimitOfAgreementUpper.Should().BeApproximately(4.7719m, 0.001m);
    }

    [Fact]
    public void Editing_data_invalidates_a_prior_calculation()
    {
        var study = New();
        study.AddPair(1m, 2m, null);
        study.AddPair(2m, 4m, null);
        study.Calculate();
        study.State.Should().Be(MethodComparisonState.Calculated);

        study.AddPair(3m, 6m, null);
        study.State.Should().Be(MethodComparisonState.DataEntry);
        study.DemingSlope.Should().BeNull();
    }

    [Fact]
    public void Sign_off_requires_calculation_and_then_freezes_the_study()
    {
        var study = New();
        study.AddPair(1m, 2m, null);
        study.AddPair(2m, 4m, null);

        var early = () => study.SignOff(Qm, Now);
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MC-012");

        study.Calculate();
        study.SignOff(Qm, Now);
        study.State.Should().Be(MethodComparisonState.SignedOff);
        study.DomainEvents.Should().ContainSingle(e => e is MethodComparisonSignedOff);

        var mutate = () => study.AddPair(3m, 6m, null);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MC-013");
    }

    [Fact]
    public void At_least_two_pairs_are_required_and_power_flag_tracks_ep09()
    {
        var study = New();
        study.AddPair(1m, 2m, null);
        var act = () => study.Calculate();
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MC-010");

        study.MeetsRecommendedPower.Should().BeFalse();
    }
}
