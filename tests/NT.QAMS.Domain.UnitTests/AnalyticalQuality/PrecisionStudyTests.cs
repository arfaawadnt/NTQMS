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
    public void Claims_are_verified_against_the_EP15_upper_verification_limit()
    {
        // Repeatability: CV ≈ 10.88 %, df = 2. Claim 5 % → UVL = 5·√(χ²(0.95,2)/2)
        // = 5·√(5.9915/2) = 8.654 % → 10.88 % exceeds it: claim NOT verified.
        // Within-lab: CV ≈ 23.08 % exceeds the 20 % claim, but with only
        // ν ≈ 1.26 Satterthwaite df the UVL is far above 23 % — EP15 cannot
        // reject the claim on so little data, so it is verified.
        var study = New(repClaim: 5m, wlClaim: 20m);
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 14m);
        study.AddMeasurement("B", 16m);

        study.Calculate();

        study.RepeatabilityDf.Should().Be(2m);
        study.RepeatabilityVerificationLimitCvPct.Should().BeApproximately(8.6541m, 0.0005m);
        study.MeetsRepeatabilityClaim.Should().BeFalse();

        // MSW = 2 (df 2), MSB = 16 (df 1), n₀ = 2: ν = 9² / (1²/2 + 8²/1) = 81/64.5.
        study.WithinLabDf.Should().Be(1.26m);
        study.WithinLabCvPct.Should().BeGreaterThan(study.ClaimedWithinLabCvPct!.Value);
        study.WithinLabVerificationLimitCvPct.Should().BeGreaterThan(study.WithinLabCvPct!.Value);
        study.MeetsWithinLabClaim.Should().BeTrue();
    }

    [Fact]
    public void Observed_cv_below_the_claim_always_verifies_it()
    {
        var study = New(repClaim: 15m);
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 14m);
        study.AddMeasurement("B", 16m);

        study.Calculate();

        // The UVL is never below the claim, so CV 10.88 % ≤ 15 % passes.
        study.RepeatabilityVerificationLimitCvPct.Should().BeGreaterThanOrEqualTo(15m);
        study.MeetsRepeatabilityClaim.Should().BeTrue();
        study.MeetsWithinLabClaim.Should().BeNull(); // no claim, no verdict
    }

    [Fact]
    public void Days_and_runs_design_runs_the_two_level_nested_anova()
    {
        // Day 1: run 1 {10,12}, run 2 {14,16} → day mean 13
        // Day 2: run 1 {20,22}, run 2 {24,26} → day mean 23, grand 18.
        // SSE = 8 (df 4) → MSE 2; SSrun = 32 (df 2) → MSrun 16; SSday = 200 (df 1).
        // S²r = 2, S²run = (16−2)/2 = 7, S²day = (200−16)/4 = 46, S²WL = 55.
        var study = NestedTwoByTwoByTwo();

        study.Calculate();

        study.Design.Should().Be(PrecisionDesign.DaysAndRuns);
        study.GrandMean.Should().Be(18m);
        study.RepeatabilitySd.Should().BeApproximately(1.4142m, 0.0005m);
        study.BetweenRunSd.Should().BeApproximately(2.6458m, 0.0005m);
        study.BetweenDaySd.Should().BeApproximately(6.7823m, 0.0005m);
        study.WithinLabSd.Should().BeApproximately(7.4162m, 0.0005m);
        study.WithinLabCvPct.Should().BeApproximately(41.2011m, 0.001m);
        study.RepeatabilityDf.Should().Be(4m);
        // Satterthwaite over ½·MSE + ¼·MSrun + ¼·MSday = 1 + 4 + 50:
        // ν = 55² / (1²/4 + 4²/2 + 50²/1) = 3025 / 2508.25.
        study.WithinLabDf.Should().Be(1.21m);
    }

    [Fact]
    public void Confidence_interval_uses_chi_square_quantiles_on_the_component_df()
    {
        // S_r = √2 with ν = 4: 95 % CI = S·√(4/χ²₀.₉₇₅) … S·√(4/χ²₀.₀₂₅)
        // = √2·√(4/11.1433) … √2·√(4/0.48442) = 0.8473 … 4.0638 → /18 → 4.707 % … 22.577 %.
        var study = NestedTwoByTwoByTwo();

        study.Calculate();

        study.RepeatabilityCvLowerPct.Should().BeApproximately(4.7072m, 0.001m);
        study.RepeatabilityCvUpperPct.Should().BeApproximately(22.5768m, 0.001m);
        study.WithinLabCvLowerPct.Should().BeLessThan(study.WithinLabCvPct!.Value);
        study.WithinLabCvUpperPct.Should().BeGreaterThan(study.WithinLabCvPct!.Value);
    }

    [Fact]
    public void One_run_per_day_is_the_days_only_design_with_a_between_day_component()
    {
        var study = New();
        study.AddMeasurement("R1", 10m, "D1", PrecisionConditions.None);
        study.AddMeasurement("R1", 12m, "D1", PrecisionConditions.None);
        study.AddMeasurement("R1", 14m, "D2", PrecisionConditions.None);
        study.AddMeasurement("R1", 16m, "D2", PrecisionConditions.None);

        study.Calculate();

        study.Design.Should().Be(PrecisionDesign.DaysOnly);
        study.BetweenDaySd.Should().BeApproximately(2.6458m, 0.0005m);
        study.BetweenRunSd.Should().BeNull();
        study.WithinLabSd.Should().Be(3m);
    }

    [Fact]
    public void Runs_are_nested_within_days_so_equal_run_labels_on_different_days_differ()
    {
        var study = NestedTwoByTwoByTwo();

        study.RunSummaries().Should().HaveCount(4);
        study.ConditionsCoverage().Runs.Should().Be(4);
        study.ConditionsCoverage().Days.Should().Be(2);
    }

    [Fact]
    public void A_design_cannot_be_partly_day_labelled_or_unbalanced()
    {
        var partly = New();
        partly.AddMeasurement("R1", 10m, "D1", PrecisionConditions.None);
        partly.AddMeasurement("R1", 12m, "D1", PrecisionConditions.None);
        partly.AddMeasurement("R2", 14m);
        partly.AddMeasurement("R2", 16m);
        partly.Invoking(s => s.Calculate()).Should().Throw<DomainException>().Which.Code.Should().Be("PR-015");

        var unbalanced = NestedTwoByTwoByTwo();
        unbalanced.AddMeasurement("R2", 27m, "D2", PrecisionConditions.None);
        unbalanced.Invoking(s => s.Calculate()).Should().Throw<DomainException>().Which.Code.Should().Be("PR-014");
    }

    [Fact]
    public void Conditions_coverage_counts_the_intermediate_precision_factors_spanned()
    {
        var study = New();
        for (var day = 1; day <= 5; day++)
        {
            for (var rep = 1; rep <= 5; rep++)
            {
                study.AddMeasurement("R1", 5m + day * 0.1m + rep * 0.01m, $"D{day}", new PrecisionConditions(
                    day % 2 == 0 ? "ana" : "ANA ", "Cobas-1", day <= 3 ? "LOT-A" : "LOT-B", "CAL-7",
                    new DateOnly(2026, 10, day)));
            }
        }

        var coverage = study.ConditionsCoverage();

        coverage.Days.Should().Be(5);
        coverage.Operators.Should().Be(1); // trimmed, case-insensitive
        coverage.Instruments.Should().Be(1);
        coverage.ReagentLots.Should().Be(2);
        coverage.CalibratorLots.Should().Be(1);
        coverage.FirstMeasuredOn.Should().Be(new DateOnly(2026, 10, 1));
        coverage.LastMeasuredOn.Should().Be(new DateOnly(2026, 10, 5));
        coverage.MeetsEp15MinimumDesign.Should().BeTrue(); // 5 days × 5 = 25 results
    }

    [Fact]
    public void A_series_splits_the_verification_alpha_across_its_levels()
    {
        var single = PrecisionStudy.Configure("PR-1", "Glucose", "mmol/L", "L1", 5m, null);
        var paired = PrecisionStudy.Configure("PR-2", "Glucose", "mmol/L", "L1", 5m, null, "PRS-2026-0001", 2);
        foreach (var s in new[] { single, paired })
        {
            s.AddMeasurement("A", 10m);
            s.AddMeasurement("A", 12m);
            s.AddMeasurement("B", 14m);
            s.AddMeasurement("B", 16m);
            s.Calculate();
        }

        // L = 2 tests each level at α/2 = 2.5 %: χ²(0.975, 2) = 7.3778 → 5·√(7.3778/2) = 9.603 %.
        paired.RepeatabilityVerificationLimitCvPct.Should().BeApproximately(9.6033m, 0.0005m);
        paired.RepeatabilityVerificationLimitCvPct.Should().BeGreaterThan(single.RepeatabilityVerificationLimitCvPct!.Value);
        paired.SeriesRef.Should().Be("PRS-2026-0001");
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData("PRS-1", 1)]
    [InlineData("PRS-1", 11)]
    public void A_series_shape_must_be_consistent(string? seriesRef, int levelCount)
    {
        var configure = () => PrecisionStudy.Configure("PR-1", "Glucose", "mmol/L", "L1", null, null, seriesRef, levelCount);
        configure.Should().Throw<DomainException>().Which.Code.Should().Be("PR-016");
    }

    private static PrecisionStudy NestedTwoByTwoByTwo()
    {
        var study = New();
        foreach (var (day, run, value) in new[]
                 {
                     ("D1", "R1", 10m), ("D1", "R1", 12m), ("D1", "R2", 14m), ("D1", "R2", 16m),
                     ("D2", "R1", 20m), ("D2", "R1", 22m), ("D2", "R2", 24m), ("D2", "R2", 26m),
                 })
        {
            study.AddMeasurement(run, value, day, PrecisionConditions.None);
        }

        return study;
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

    [Fact]
    public void Multi_site_design_adds_a_between_site_component_and_the_reproducibility_sd()
    {
        // Site A: D1 {10,12}, D2 {14,16} → site mean 13; Site B: D1 {20,22}, D2 {24,26} → 23.
        // MSE = 2 (df 4), MS_day(site) = 16 (df 2), MS_site = 200 (df 1).
        // σ²e = 2, σ²day = (16−2)/2 = 7, σ²site = (200−16)/4 = 46.
        // S²WL = 9 → S_WL = 3, CV 3/18 = 16.667 %; S²R = 55 → S_R = 7.4162, CV 41.201 %.
        var study = TwoSitesTwoDays(reproducibilityClaim: 10m);

        study.Calculate();

        study.Design.Should().Be(PrecisionDesign.SitesAndDays);
        study.RepeatabilitySd.Should().BeApproximately(1.4142m, 0.0005m);
        study.BetweenDaySd.Should().BeApproximately(2.6458m, 0.0005m);
        study.BetweenRunSd.Should().BeNull();
        study.BetweenSiteSd.Should().BeApproximately(6.7823m, 0.0005m);
        study.WithinLabSd.Should().Be(3m);
        study.WithinLabCvPct.Should().BeApproximately(16.6667m, 0.001m);
        study.ReproducibilitySd.Should().BeApproximately(7.4162m, 0.0005m);
        study.ReproducibilityCvPct.Should().BeApproximately(41.2011m, 0.001m);
        // ν_WL = 9² / ((½·16)²/2 + (½·2)²/4) = 81 / 32.25; ν_R = 55² / (50²/1 + 4²/2 + 1²/4) = 3025 / 2508.25.
        study.WithinLabDf.Should().Be(2.51m);
        study.ReproducibilityDf.Should().Be(1.21m);
        study.ReproducibilityCvLowerPct.Should().BeLessThan(study.ReproducibilityCvPct!.Value);
        study.ReproducibilityCvUpperPct.Should().BeGreaterThan(study.ReproducibilityCvPct!.Value);
    }

    [Fact]
    public void A_reproducibility_claim_is_verified_directly_by_multi_site_data()
    {
        var fails = TwoSitesTwoDays(reproducibilityClaim: 10m);
        fails.Calculate();

        // UVL = 10 · √(χ²(0.95, 1.206) / 1.206) is far below the observed 41.2 %.
        fails.ReproducibilityVerificationLimitCvPct.Should().BeLessThan(fails.ReproducibilityCvPct!.Value);
        fails.MeetsReproducibilityClaim.Should().BeFalse();
        fails.WithinLabConsistentWithReproducibilityClaim.Should().BeNull("multi-site data verifies the claim directly");

        var met = TwoSitesTwoDays(reproducibilityClaim: 45m);
        met.Calculate();
        met.MeetsReproducibilityClaim.Should().BeTrue();
    }

    [Fact]
    public void A_single_site_study_checks_its_within_lab_cv_against_the_reproducibility_claim()
    {
        // Within-lab CV 23.08 % at ν ≈ 1.26; a 5 % reproducibility claim gives
        // UVL = 5 · √(χ²(0.95, 1.256) / 1.256) ≈ 9.6 % — the lab's own imprecision
        // is not consistent with the manufacturer's between-laboratory figure.
        var study = PrecisionStudy.Configure("PR-1", "Glucose", "mmol/L", "L1", null, null, null, 1, 5m);
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 14m);
        study.AddMeasurement("B", 16m);

        study.Calculate();

        study.ReproducibilitySd.Should().BeNull();
        study.MeetsReproducibilityClaim.Should().BeNull();
        study.ReproducibilityVerificationLimitCvPct.Should().BeLessThan(study.WithinLabCvPct!.Value);
        study.WithinLabConsistentWithReproducibilityClaim.Should().BeFalse();
    }

    [Fact]
    public void A_multi_site_study_labels_every_replicate_spans_two_sites_and_is_balanced()
    {
        var partly = TwoSitesTwoDays(reproducibilityClaim: null);
        partly.AddMeasurement("R1", 18m, "D1", PrecisionConditions.None);
        partly.Invoking(s => s.Calculate()).Should().Throw<DomainException>().Which.Code.Should().Be("PR-017");

        var oneSite = New();
        foreach (var (day, value) in new[] { ("D1", 10m), ("D1", 12m), ("D2", 14m), ("D2", 16m) })
        {
            oneSite.AddMeasurement("R1", value, day, PrecisionConditions.None, "Main lab");
        }

        oneSite.Invoking(s => s.Calculate()).Should().Throw<DomainException>().Which.Code.Should().Be("PR-018");

        var unbalanced = TwoSitesTwoDays(reproducibilityClaim: null);
        unbalanced.AddMeasurement("R1", 27m, "D3", PrecisionConditions.None, "Site B");
        unbalanced.AddMeasurement("R1", 28m, "D3", PrecisionConditions.None, "Site B");
        unbalanced.Invoking(s => s.Calculate()).Should().Throw<DomainException>().Which.Code.Should().Be("PR-014");
    }

    [Fact]
    public void Equal_day_labels_at_different_sites_are_different_days()
    {
        var study = TwoSitesTwoDays(reproducibilityClaim: null);

        var coverage = study.ConditionsCoverage();

        coverage.Sites.Should().Be(2);
        coverage.Days.Should().Be(4);
        coverage.MeetsMultiSiteMinimumDesign.Should().BeFalse();
        study.RunSummaries().Select(r => r.SiteLabel).Distinct().Should().BeEquivalentTo(["Site A", "Site B"]);
    }

    [Fact]
    public void Three_sites_of_five_by_five_meet_the_EP05_multi_site_minimum()
    {
        var study = New();
        foreach (var site in new[] { "S1", "S2", "S3" })
        {
            for (var day = 1; day <= 5; day++)
            {
                for (var rep = 1; rep <= 5; rep++)
                {
                    study.AddMeasurement("R1", 5m + day * 0.01m + rep * 0.001m, $"D{day}", PrecisionConditions.None, site);
                }
            }
        }

        study.ConditionsCoverage().MeetsMultiSiteMinimumDesign.Should().BeTrue();
        study.Calculate();
        study.Design.Should().Be(PrecisionDesign.SitesAndDays);
        study.ReproducibilityDf.Should().BeGreaterThan(0m);
    }

    private static PrecisionStudy TwoSitesTwoDays(decimal? reproducibilityClaim)
    {
        var study = PrecisionStudy.Configure("PR-2026-0007", "Glucose", "mmol/L", "L1", null, null, null, 1, reproducibilityClaim);
        foreach (var (site, day, value) in new[]
                 {
                     ("Site A", "D1", 10m), ("Site A", "D1", 12m), ("Site A", "D2", 14m), ("Site A", "D2", 16m),
                     ("Site B", "D1", 20m), ("Site B", "D1", 22m), ("Site B", "D2", 24m), ("Site B", "D2", 26m),
                 })
        {
            study.AddMeasurement("R1", value, day, PrecisionConditions.None, site);
        }

        return study;
    }
}
