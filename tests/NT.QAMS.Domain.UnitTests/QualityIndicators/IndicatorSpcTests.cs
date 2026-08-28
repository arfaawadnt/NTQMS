using FluentAssertions;
using NT.QAMS.Domain.QualityIndicators;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.QualityIndicators;

public class IndicatorSpcTests
{
    [Fact]
    public void Too_few_points_yields_no_limits()
    {
        IndicatorSpc.Analyze([1m, 2m, 3m]).HasLimits.Should().BeFalse();
    }

    [Fact]
    public void Stable_series_has_no_special_cause()
    {
        var values = new[] { 50m, 51m, 49m, 50m, 51m, 49m, 50m, 50m };
        var a = IndicatorSpc.Analyze(values);

        a.HasLimits.Should().BeTrue();
        a.Points.Should().OnlyContain(p => !p.SpecialCause);
        a.Ucl.Should().BeGreaterThan(a.Mean);
        a.Lcl.Should().BeLessThan(a.Mean);
    }

    [Fact]
    public void R2_flags_the_second_of_two_opening_points_beyond_two_sigma()
    {
        // M-17: the R2 window skipped the series opening (end < 2), so a
        // Beyond, Beyond, In-control start was never flagged even though two
        // of the first "three" points sit past 2σ.
        var values = new[] { 64m, 65m, 50m, 50m, 51m, 49m, 50m, 50m, 49m, 51m, 50m, 50m };
        var a = IndicatorSpc.Analyze(values);

        a.Points[0].Value.Should().BeGreaterThan(a.Upper2Sigma, "precondition: the opening pair sits past 2σ");
        a.Points[1].Value.Should().BeGreaterThan(a.Upper2Sigma, "precondition: the opening pair sits past 2σ");
        a.Points[1].Rules.Should().Contain("R2");
        a.Points[1].SpecialCause.Should().BeTrue();
    }

    [Fact]
    public void R1_flags_a_point_beyond_three_sigma()
    {
        // Many tight points so one outlier does not inflate its own limits out of reach.
        var values = new List<decimal>();
        for (var i = 0; i < 15; i++) { values.Add(50m); }
        values.Add(70m); // clear outlier at the end
        var a = IndicatorSpc.Analyze(values);

        a.Points[^1].Rules.Should().Contain("R1");
        a.Points[^1].SpecialCause.Should().BeTrue();
    }

    [Fact]
    public void R3_flags_eight_consecutive_on_the_same_side()
    {
        // Eight straight points above the centre line established by the whole series.
        var values = new[] { 10m, 10m, 10m, 10m, 20m, 21m, 22m, 23m, 24m, 25m, 26m, 27m };
        var a = IndicatorSpc.Analyze(values);

        a.Points.Any(p => p.Rules.Contains("R3")).Should().BeTrue();
    }

    [Fact]
    public void R4_flags_six_point_monotonic_trend()
    {
        var values = new[] { 50m, 40m, 41m, 42m, 43m, 44m, 45m, 46m };
        var a = IndicatorSpc.Analyze(values);

        a.Points.Any(p => p.Rules.Contains("R4")).Should().BeTrue();
    }
}
