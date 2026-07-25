using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

/// <summary>Math + workflow anchors for the five SOW-compliance analytical modules.</summary>
public sealed class ComplianceModuleTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    // ── Outlier screening ──────────────────────────────────────────────────────

    [Fact]
    public void Outlier_screening_flags_the_extreme_point_by_tukey_and_modified_z()
    {
        var s = OutlierScreening.Configure("OUT-1", "Calibrator replicates", "mmol/L");
        foreach (var v in new[] { 10m, 11m, 12m, 13m, 100m })
        {
            s.AddPoint(v, null);
        }

        s.Calculate();

        s.OutlierCount.Should().Be(1);
        s.Median.Should().Be(12m);
        var extreme = s.PointResults().Single(r => r.Value == 100m);
        extreme.IsOutlier.Should().BeTrue();
        s.PointResults().Where(r => r.Value != 100m).Should().OnlyContain(r => !r.IsOutlier);
    }

    [Fact]
    public void Outlier_screening_needs_four_points_and_freezes_on_sign_off()
    {
        var s = OutlierScreening.Configure("OUT-1", "Set", "u");
        s.AddPoint(1m, null); s.AddPoint(2m, null); s.AddPoint(3m, null);
        var tooFew = () => s.Calculate();
        tooFew.Should().Throw<DomainException>().Which.Code.Should().Be("OUT-010");

        s.AddPoint(4m, null);
        s.Calculate();
        s.SignOff(Qm, Now);
        var mutate = () => s.AddPoint(5m, null);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("OUT-012");
    }

    // ── Carryover ──────────────────────────────────────────────────────────────

    [Fact]
    public void Carryover_uses_first_low_over_high_minus_steady()
    {
        // High 1000; lows 12, 10, 10 → (12−10)/(1000−10)×100 ≈ 0.2020%.
        var s = CarryoverStudy.Configure("CAR-1", "AST", "U/L", allowableCarryoverPct: 1m);
        s.AddReading(CarryoverSampleKind.High, 1, 1000m);
        s.AddReading(CarryoverSampleKind.Low, 1, 12m);
        s.AddReading(CarryoverSampleKind.Low, 2, 10m);
        s.AddReading(CarryoverSampleKind.Low, 3, 10m);

        s.Calculate();

        s.MeanHigh.Should().Be(1000m);
        s.SteadyLow.Should().Be(10m);
        s.CarryoverPct.Should().BeApproximately(0.2020m, 0.0005m);
        s.Passes.Should().BeTrue();
    }

    // ── Lot-to-lot ─────────────────────────────────────────────────────────────

    [Fact]
    public void Lot_comparison_mean_bias_percent_and_verdict()
    {
        // Current mean 10, new mean 10.5 → +5% bias; allowable 3.5% → fails.
        var s = LotComparisonStudy.Configure("LOT-1", "Glucose", "mmol/L", "L100", "L101", allowableBiasPct: 3.5m);
        s.AddPair(10m, 10.5m, "S1");
        s.AddPair(10m, 10.5m, "S2");
        s.AddPair(10m, 10.5m, "S3");

        s.Calculate();

        s.MeanBiasPct.Should().Be(5m);
        s.Passes.Should().BeFalse();
    }

    // ── Interference (EP07) ─────────────────────────────────────────────────────

    [Fact]
    public void Interference_bias_per_interferent_flags_significant_ones()
    {
        // Control mean 100; haemolysis test mean 90 → −10% (significant at 5%);
        // lipaemia test mean 102 → +2% (not significant).
        var s = InterferenceStudy.Configure("INT-1", "Bilirubin", "µmol/L", allowableBiasPct: 5m);
        foreach (var v in new[] { 100m, 100m, 100m }) { s.AddControl(v); }
        foreach (var v in new[] { 88m, 92m }) { s.AddTest("Haemolysis", v); }
        foreach (var v in new[] { 101m, 103m }) { s.AddTest("Lipaemia", v); }

        s.Calculate();

        s.ControlMean.Should().Be(100m);
        s.SignificantCount.Should().Be(1);
        var results = s.Results();
        results.Single(r => r.Interferent == "Haemolysis").BiasPct.Should().Be(-10m);
        results.Single(r => r.Interferent == "Haemolysis").SignificantInterference.Should().BeTrue();
        results.Single(r => r.Interferent == "Lipaemia").SignificantInterference.Should().BeFalse();
    }

    // ── Instrument comparability ────────────────────────────────────────────────

    [Fact]
    public void Instrument_comparability_bias_vs_reference_per_instrument()
    {
        var s = InstrumentComparabilityStudy.Configure("ICP-1", "Sodium", "mmol/L", "Analyzer-A", allowableBiasPct: 2m);
        // Reference A: S1=140, S2=100.
        s.AddReading("Analyzer-A", "S1", 140m);
        s.AddReading("Analyzer-A", "S2", 100m);
        // B agrees (mean bias 0) → comparable.
        s.AddReading("Analyzer-B", "S1", 140m);
        s.AddReading("Analyzer-B", "S2", 100m);
        // C reads high: S1=144 (+2.857%), S2=103 (+3%) → mean ~+2.93% → not comparable at 2%.
        s.AddReading("Analyzer-C", "S1", 144m);
        s.AddReading("Analyzer-C", "S2", 103m);

        s.Calculate();

        s.InstrumentCount.Should().Be(2);
        var results = s.Results();
        results.Single(r => r.Instrument == "Analyzer-B").MeanBiasPct.Should().Be(0m);
        results.Single(r => r.Instrument == "Analyzer-B").Comparable.Should().BeTrue();
        results.Single(r => r.Instrument == "Analyzer-C").Comparable.Should().BeFalse();
        s.NonComparableCount.Should().Be(1);
    }
}
