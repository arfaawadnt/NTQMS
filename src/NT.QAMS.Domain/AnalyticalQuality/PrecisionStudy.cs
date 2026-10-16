using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum PrecisionState { DataEntry, Calculated, SignedOff }

/// <summary>
/// The experimental layout the replicates were entered in, resolved at
/// Calculate from how they are labelled (CLSI EP05-A3 / EP15-A3):
/// <list type="bullet">
/// <item><see cref="RunsOnly"/> — replicates grouped into runs, no day label: one-way ANOVA (runs).</item>
/// <item><see cref="DaysOnly"/> — day-labelled, one run per day (the EP15 5 × 5 design): one-way ANOVA (days).</item>
/// <item><see cref="DaysAndRuns"/> — several runs within each day (the EP05 20 × 2 × 2 design): two-level nested ANOVA.</item>
/// <item><see cref="SitesAndRuns"/>, <see cref="SitesAndDays"/>, <see cref="SitesDaysAndRuns"/> — the same
/// layouts repeated at several sites (the EP05-A3 multi-site design, e.g. 3 sites × 5 days × 5 replicates):
/// nested ANOVA with sites on top, adding a between-site component and the reproducibility SD.</item>
/// </list>
/// </summary>
public enum PrecisionDesign { RunsOnly, DaysOnly, DaysAndRuns, SitesAndRuns, SitesAndDays, SitesDaysAndRuns }

/// <summary>
/// The measurement conditions a replicate was obtained under. Recording them is
/// what lets a reviewer see which sources of variation the within-laboratory
/// estimate actually spans (ISO 5725-3 intermediate-precision factors:
/// time, operator, equipment, calibration, reagent lot).
/// </summary>
public sealed record PrecisionConditions(
    string? Operator, string? Instrument, string? ReagentLot, string? CalibratorLot, DateOnly? MeasuredOn)
{
    /// <summary>No conditions recorded.</summary>
    public static readonly PrecisionConditions None = new(null, null, null, null, null);
}

/// <summary>One replicate belonging to a named run (optionally within a named day, within a named site).</summary>
public sealed class PrecisionMeasurement : Entity
{
    internal PrecisionMeasurement(
        string runLabel, decimal value, string? dayLabel, string? siteLabel, PrecisionConditions conditions)
    {
        RunLabel = runLabel;
        Value = value;
        DayLabel = dayLabel;
        SiteLabel = siteLabel;
        Operator = conditions.Operator;
        Instrument = conditions.Instrument;
        ReagentLot = conditions.ReagentLot;
        CalibratorLot = conditions.CalibratorLot;
        MeasuredOn = conditions.MeasuredOn;
    }

    private PrecisionMeasurement() { RunLabel = null!; }

    public string RunLabel { get; private set; }
    public decimal Value { get; private set; }
    /// <summary>The day the run belongs to; runs are nested within days. Null in a runs-only design.</summary>
    public string? DayLabel { get; private set; }
    /// <summary>The site (laboratory) the day/run belongs to; null in a single-site study.</summary>
    public string? SiteLabel { get; private set; }
    public string? Operator { get; private set; }
    public string? Instrument { get; private set; }
    public string? ReagentLot { get; private set; }
    public string? CalibratorLot { get; private set; }
    public DateOnly? MeasuredOn { get; private set; }
}

/// <summary>Component summary for a run — mean and replicate count, for reporting.</summary>
public sealed record PrecisionRunSummary(
    string? DayLabel, string RunLabel, int ReplicateCount, decimal Mean, string? SiteLabel);

/// <summary>
/// How many distinct levels of each precision factor the data spans —
/// derived from the replicates, never stored.
/// </summary>
public sealed record PrecisionConditionsCoverage(
    int Days, int Runs, int Operators, int Instruments, int ReagentLots, int CalibratorLots,
    DateOnly? FirstMeasuredOn, DateOnly? LastMeasuredOn, bool MeetsEp15MinimumDesign,
    int Sites, bool MeetsMultiSiteMinimumDesign);

/// <summary>
/// Imprecision study (CLSI EP05-A3 establishment / EP15-A3 verification) at one
/// concentration level. Replicates are grouped into runs, optionally runs into
/// days and days into sites; Calculate applies the matching random-effects
/// ANOVA (see <see cref="PrecisionDesign"/>) to separate repeatability
/// (within-run) from the between-run, between-day and between-site components,
/// combining them into the within-laboratory imprecision (intermediate
/// precision, S_WL — pooled over sites) and, for a multi-site study, the
/// <b>reproducibility</b> SD (S_R = S_WL plus the between-site component,
/// ISO 5725-2). Each SD is reported with its CV, its degrees of freedom
/// (Satterthwaite for combined SDs) and a 95 % χ² confidence interval. A
/// manufacturer claim is verified the EP15-A3 way: the claim is accepted when
/// the observed CV does not exceed UVL = claim · √(χ²(1 − α/L, ν) / ν), α = 5 %
/// split across the L levels of the series. A reproducibility claim is verified
/// against the study's own reproducibility only when it spans several sites;
/// a single-site study instead checks that its within-lab CV is consistent with
/// the claim (a laboratory's own imprecision cannot plausibly exceed the
/// between-laboratory figure). Measurements are immutable evidence; statistics
/// are derivable-only; sign-off freezes the study.
/// </summary>
public sealed class PrecisionStudy : AggregateRoot, ITenantScoped
{
    public const int MinimumRuns = 2;
    public const int MinimumReplicatesPerRun = 2;
    /// <summary>A multi-site study needs at least two sites to estimate a between-site component.</summary>
    public const int MinimumSites = 2;
    /// <summary>Family-wise false-rejection rate of a claim verification (EP15-A3).</summary>
    public const double VerificationAlpha = 0.05;
    /// <summary>Two-sided confidence level of the reported SD/CV intervals.</summary>
    public const double ConfidenceLevel = 0.95;
    /// <summary>EP15-A3 minimum: 5 days (or runs) …</summary>
    public const int Ep15MinimumGroups = 5;
    /// <summary>… and 25 results in total.</summary>
    public const int Ep15MinimumResults = 25;
    /// <summary>EP05-A3 multi-site minimum: 3 sites, each meeting the 5 × 5 minimum.</summary>
    public const int MultiSiteMinimumSites = 3;
    /// <summary>Upper bound on the number of levels a study series can declare.</summary>
    public const int MaximumSeriesLevels = 10;

    private readonly List<PrecisionMeasurement> _measurements = [];

    private PrecisionStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
        Level = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    /// <summary>The concentration level under study (imprecision is level-dependent).</summary>
    public string Level { get; private set; }
    /// <summary>Links the per-level studies created together as one multi-level series; null for a stand-alone study.</summary>
    public string? SeriesRef { get; private set; }
    /// <summary>Number of levels in the series (1 when stand-alone) — the L in the α/L verification split.</summary>
    public int SeriesLevelCount { get; private set; }
    /// <summary>Optional manufacturer repeatability claim (within-run CV%).</summary>
    public decimal? ClaimedRepeatabilityCvPct { get; private set; }
    /// <summary>Optional manufacturer within-laboratory claim (total CV%).</summary>
    public decimal? ClaimedWithinLabCvPct { get; private set; }
    /// <summary>Optional manufacturer reproducibility claim (between-laboratory CV%, from its multi-site study).</summary>
    public decimal? ClaimedReproducibilityCvPct { get; private set; }
    public PrecisionState State { get; private set; }

    // Derived (null until Calculate).
    public PrecisionDesign? Design { get; private set; }
    public decimal? GrandMean { get; private set; }
    public decimal? RepeatabilitySd { get; private set; }
    public decimal? RepeatabilityCvPct { get; private set; }
    public decimal? RepeatabilityDf { get; private set; }
    public decimal? RepeatabilityCvLowerPct { get; private set; }
    public decimal? RepeatabilityCvUpperPct { get; private set; }
    /// <summary>Between-run component; null when the design has one run per day.</summary>
    public decimal? BetweenRunSd { get; private set; }
    public decimal? BetweenRunCvPct { get; private set; }
    /// <summary>Between-day component; null in a design without day labels.</summary>
    public decimal? BetweenDaySd { get; private set; }
    public decimal? BetweenDayCvPct { get; private set; }
    /// <summary>Between-site component; null in a single-site study.</summary>
    public decimal? BetweenSiteSd { get; private set; }
    public decimal? BetweenSiteCvPct { get; private set; }
    public decimal? WithinLabSd { get; private set; }
    public decimal? WithinLabCvPct { get; private set; }
    /// <summary>Satterthwaite effective degrees of freedom of S_WL.</summary>
    public decimal? WithinLabDf { get; private set; }
    public decimal? WithinLabCvLowerPct { get; private set; }
    public decimal? WithinLabCvUpperPct { get; private set; }
    /// <summary>Reproducibility SD S_R (multi-site only): S_WL plus the between-site component.</summary>
    public decimal? ReproducibilitySd { get; private set; }
    public decimal? ReproducibilityCvPct { get; private set; }
    /// <summary>Satterthwaite effective degrees of freedom of S_R.</summary>
    public decimal? ReproducibilityDf { get; private set; }
    public decimal? ReproducibilityCvLowerPct { get; private set; }
    public decimal? ReproducibilityCvUpperPct { get; private set; }
    /// <summary>EP15-A3 upper verification limit for the repeatability claim (CV%).</summary>
    public decimal? RepeatabilityVerificationLimitCvPct { get; private set; }
    /// <summary>EP15-A3 upper verification limit for the within-lab claim (CV%).</summary>
    public decimal? WithinLabVerificationLimitCvPct { get; private set; }
    /// <summary>
    /// Upper verification limit for the reproducibility claim (CV%): at the
    /// reproducibility df for a multi-site study, otherwise at the within-lab df
    /// (the consistency check).
    /// </summary>
    public decimal? ReproducibilityVerificationLimitCvPct { get; private set; }
    /// <summary>Observed repeatability CV ≤ its UVL (null without a claim).</summary>
    public bool? MeetsRepeatabilityClaim { get; private set; }
    /// <summary>Observed within-lab CV ≤ its UVL (null without a claim).</summary>
    public bool? MeetsWithinLabClaim { get; private set; }
    /// <summary>Observed reproducibility CV ≤ its UVL (null without a claim or without multi-site data).</summary>
    public bool? MeetsReproducibilityClaim { get; private set; }
    /// <summary>
    /// Single-site check: the within-lab CV does not exceed the reproducibility
    /// claim's UVL (null without a claim or for a multi-site study, which is
    /// verified directly).
    /// </summary>
    public bool? WithinLabConsistentWithReproducibilityClaim { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<PrecisionMeasurement> Measurements => _measurements.AsReadOnly();

    /// <summary>Configures a stand-alone (single-level) study.</summary>
    public static PrecisionStudy Configure(
        string studyRef, string analyte, string unit, string level,
        decimal? claimedRepeatabilityCvPct, decimal? claimedWithinLabCvPct) =>
        Configure(studyRef, analyte, unit, level, claimedRepeatabilityCvPct, claimedWithinLabCvPct, null, 1);

    /// <summary>
    /// Configures one level of a study. A multi-level series passes its shared
    /// series reference and its level count so every member verifies its
    /// claims at α / L (EP15-A3 Bonferroni split).
    /// </summary>
    public static PrecisionStudy Configure(
        string studyRef, string analyte, string unit, string level,
        decimal? claimedRepeatabilityCvPct, decimal? claimedWithinLabCvPct,
        string? seriesRef, int seriesLevelCount, decimal? claimedReproducibilityCvPct = null)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("PR-001", "An analyte is required.");
        }

        if (claimedRepeatabilityCvPct is <= 0m || claimedWithinLabCvPct is <= 0m || claimedReproducibilityCvPct is <= 0m)
        {
            throw new DomainException("PR-002", "Claimed CVs, when given, must be positive percentages.");
        }

        if (seriesLevelCount is < 1 or > MaximumSeriesLevels
            || (seriesLevelCount > 1) == string.IsNullOrWhiteSpace(seriesRef))
        {
            throw new DomainException(
                "PR-016", $"A study series links 2–{MaximumSeriesLevels} levels under one series reference; a stand-alone study has neither.");
        }

        return new PrecisionStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            Level = string.IsNullOrWhiteSpace(level) ? string.Empty : level.Trim(),
            SeriesRef = string.IsNullOrWhiteSpace(seriesRef) ? null : seriesRef.Trim(),
            SeriesLevelCount = seriesLevelCount,
            ClaimedRepeatabilityCvPct = claimedRepeatabilityCvPct,
            ClaimedWithinLabCvPct = claimedWithinLabCvPct,
            ClaimedReproducibilityCvPct = claimedReproducibilityCvPct,
            State = PrecisionState.DataEntry,
        };
    }

    /// <summary>Adds a replicate with no day or site label and no recorded conditions.</summary>
    public Guid AddMeasurement(string runLabel, decimal value) =>
        AddMeasurement(runLabel, value, null, PrecisionConditions.None);

    /// <summary>
    /// Adds a replicate. A day label nests the run within that day and a site
    /// label nests the day (or run) within that site — run "R1" on day "D01"
    /// and run "R1" on day "D02" are different runs, as are day "D01" at two sites.
    /// </summary>
    public Guid AddMeasurement(
        string runLabel, decimal value, string? dayLabel, PrecisionConditions conditions, string? siteLabel = null)
    {
        RequireEditable();
        if (string.IsNullOrWhiteSpace(runLabel))
        {
            throw new DomainException("PR-003", "A run label is required — replicates are grouped by run.");
        }

        var measurement = new PrecisionMeasurement(
            runLabel.Trim(), value, Clean(dayLabel), Clean(siteLabel),
            new PrecisionConditions(
                Clean(conditions.Operator), Clean(conditions.Instrument),
                Clean(conditions.ReagentLot), Clean(conditions.CalibratorLot), conditions.MeasuredOn));
        _measurements.Add(measurement);
        Invalidate();
        return measurement.Id;
    }

    public void RemoveMeasurement(Guid measurementId)
    {
        RequireEditable();
        var measurement = _measurements.FirstOrDefault(m => m.Id == measurementId)
            ?? throw new DomainException("PR-404", "Measurement not found.");
        _measurements.Remove(measurement);
        Invalidate();
    }

    /// <summary>
    /// Resolves the design from the labels, runs the matching random-effects
    /// ANOVA, and derives every SD/CV, df, confidence interval and claim
    /// verdict. Negative variance-component estimates are floored at 0 (EP05-A3).
    /// </summary>
    public void Calculate()
    {
        RequireEditable();
        var dayLabelled = _measurements.Count(m => m.DayLabel is not null);
        if (dayLabelled > 0 && dayLabelled < _measurements.Count)
        {
            throw new DomainException(
                "PR-015", "Either every replicate carries a day label or none does — a design cannot be partly nested.");
        }

        var siteLabelled = _measurements.Count(m => m.SiteLabel is not null);
        if (siteLabelled > 0 && siteLabelled < _measurements.Count)
        {
            throw new DomainException(
                "PR-017", "Either every replicate carries a site label or none does — a multi-site study labels every replicate with its site.");
        }

        var result = siteLabelled > 0 ? MultiSite(dayLabelled > 0)
            : dayLabelled > 0 ? DayLabelled()
            : OneWay(PrecisionDesign.RunsOnly, m => m.RunLabel);

        var grand = result.GrandMean;
        Design = result.Design;
        GrandMean = Round(grand);

        var repeatabilitySd = Math.Sqrt(result.RepeatabilityVariance);
        RepeatabilitySd = Round(repeatabilitySd);
        RepeatabilityCvPct = Cv(repeatabilitySd, grand);
        RepeatabilityDf = RoundDf(result.RepeatabilityDf);

        (BetweenRunSd, BetweenRunCvPct) = Component(result.BetweenRunVariance, grand);
        (BetweenDaySd, BetweenDayCvPct) = Component(result.BetweenDayVariance, grand);
        (BetweenSiteSd, BetweenSiteCvPct) = Component(result.BetweenSiteVariance, grand);

        var withinLabSd = Math.Sqrt(result.WithinLabVariance);
        WithinLabSd = Round(withinLabSd);
        WithinLabCvPct = Cv(withinLabSd, grand);
        WithinLabDf = RoundDf(result.WithinLabDf);

        (RepeatabilityCvLowerPct, RepeatabilityCvUpperPct) = CvInterval(repeatabilitySd, grand, result.RepeatabilityDf);
        (WithinLabCvLowerPct, WithinLabCvUpperPct) = CvInterval(withinLabSd, grand, result.WithinLabDf);

        if (result.ReproducibilityVariance is { } reproducibilityVariance && result.ReproducibilityDf is { } reproducibilityDf)
        {
            var reproducibilitySd = Math.Sqrt(reproducibilityVariance);
            ReproducibilitySd = Round(reproducibilitySd);
            ReproducibilityCvPct = Cv(reproducibilitySd, grand);
            ReproducibilityDf = RoundDf(reproducibilityDf);
            (ReproducibilityCvLowerPct, ReproducibilityCvUpperPct) = CvInterval(reproducibilitySd, grand, reproducibilityDf);
        }

        RepeatabilityVerificationLimitCvPct = VerificationLimit(ClaimedRepeatabilityCvPct, result.RepeatabilityDf);
        WithinLabVerificationLimitCvPct = VerificationLimit(ClaimedWithinLabCvPct, result.WithinLabDf);
        MeetsRepeatabilityClaim = Meets(RepeatabilityCvPct, RepeatabilityVerificationLimitCvPct);
        MeetsWithinLabClaim = Meets(WithinLabCvPct, WithinLabVerificationLimitCvPct);

        // A reproducibility claim is verified directly by multi-site data; a
        // single site can only show its own imprecision is consistent with it.
        ReproducibilityVerificationLimitCvPct = VerificationLimit(
            ClaimedReproducibilityCvPct, result.ReproducibilityDf ?? result.WithinLabDf);
        MeetsReproducibilityClaim = result.ReproducibilityDf is null
            ? null : Meets(ReproducibilityCvPct, ReproducibilityVerificationLimitCvPct);
        WithinLabConsistentWithReproducibilityClaim = result.ReproducibilityDf is null
            ? Meets(WithinLabCvPct, ReproducibilityVerificationLimitCvPct) : null;

        State = PrecisionState.Calculated;
    }

    /// <summary>Per-run means for the workspace table/plot — derived, never stored.</summary>
    public IReadOnlyList<PrecisionRunSummary> RunSummaries() =>
        _measurements
            .GroupBy(m => (m.SiteLabel, m.DayLabel, m.RunLabel))
            .OrderBy(g => g.Key.SiteLabel, StringComparer.Ordinal)
            .ThenBy(g => g.Key.DayLabel, StringComparer.Ordinal)
            .ThenBy(g => g.Key.RunLabel, StringComparer.Ordinal)
            .Select(g => new PrecisionRunSummary(
                g.Key.DayLabel, g.Key.RunLabel, g.Count(), Math.Round(g.Average(m => m.Value), 4), g.Key.SiteLabel))
            .ToList();

    /// <summary>Which precision factors the replicates span — derived, never stored.</summary>
    public PrecisionConditionsCoverage ConditionsCoverage()
    {
        var sites = _measurements.Where(m => m.SiteLabel is not null).Select(m => m.SiteLabel).Distinct().Count();
        var days = _measurements.Where(m => m.DayLabel is not null).Select(m => (m.SiteLabel, m.DayLabel)).Distinct().Count();
        var runs = _measurements.Select(m => (m.SiteLabel, m.DayLabel, m.RunLabel)).Distinct().Count();
        var dates = _measurements.Where(m => m.MeasuredOn is not null).Select(m => m.MeasuredOn!.Value).ToList();
        var groups = days > 0 ? days : runs;

        // Every site must itself meet the 5 × 5 minimum (EP05-A3 multi-site: 3 × 5 × 5).
        var meetsMultiSite = sites >= MultiSiteMinimumSites && _measurements
            .GroupBy(m => m.SiteLabel)
            .All(site => site.Count() >= Ep15MinimumResults
                         && (site.Any(m => m.DayLabel is not null)
                             ? site.Select(m => m.DayLabel).Distinct().Count()
                             : site.Select(m => m.RunLabel).Distinct().Count()) >= Ep15MinimumGroups);

        return new PrecisionConditionsCoverage(
            days, runs,
            DistinctCount(m => m.Operator), DistinctCount(m => m.Instrument),
            DistinctCount(m => m.ReagentLot), DistinctCount(m => m.CalibratorLot),
            dates.Count > 0 ? dates.Min() : null, dates.Count > 0 ? dates.Max() : null,
            groups >= Ep15MinimumGroups && _measurements.Count >= Ep15MinimumResults,
            sites, meetsMultiSite);
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001");
        if (State != PrecisionState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "PR-012", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = PrecisionState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new PrecisionStudySignedOff(Id, StudyRef, Analyte, TenantId));
    }

    /// <summary>
    /// One-way random-effects ANOVA over <paramref name="groupKey"/> (runs, or
    /// days when each day holds one run). MSW is the repeatability variance and
    /// (MSB − MSW)/n₀ the between-group component; S_WL² = MSW + that component,
    /// with Satterthwaite df over the two mean squares. Tolerates unbalanced groups.
    /// </summary>
    private AnovaResult OneWay(PrecisionDesign design, Func<PrecisionMeasurement, string> groupKey)
    {
        var groups = _measurements
            .GroupBy(groupKey)
            .Select(g => g.Select(m => (double)m.Value).ToArray())
            .ToList();
        RequireMinimums(groups.Count, groups.Count == 0 ? 0 : groups.Min(g => g.Length));

        var k = groups.Count;
        var n = _measurements.Count;
        var grand = groups.SelectMany(g => g).Average();

        double ssWithin = 0, ssBetween = 0;
        foreach (var group in groups)
        {
            var mean = group.Average();
            ssWithin += group.Sum(v => (v - mean) * (v - mean));
            ssBetween += group.Length * (mean - grand) * (mean - grand);
        }

        var dfWithin = (double)(n - k);
        var dfBetween = (double)(k - 1);
        var msWithin = ssWithin / dfWithin;
        var msBetween = ssBetween / dfBetween;

        // n₀ = (N − Σnᵢ²/N)/(k−1); equals the common replicate count for a balanced design.
        var n0 = (n - groups.Sum(g => (double)g.Length * g.Length) / n) / dfBetween;
        var betweenVar = Math.Max(0, (msBetween - msWithin) / n0);

        var terms = new List<MeanSquareTerm> { new(msWithin, dfWithin, 1) };
        if (betweenVar > 0)
        {
            terms[0] = terms[0] with { Coefficient = 1 - 1 / n0 };
            terms.Add(new MeanSquareTerm(msBetween, dfBetween, 1 / n0));
        }

        return new AnovaResult(
            design, grand, msWithin, dfWithin,
            design == PrecisionDesign.RunsOnly ? betweenVar : null,
            design == PrecisionDesign.DaysOnly ? betweenVar : null,
            null, msWithin + betweenVar, Satterthwaite(terms, dfWithin), null, null);
    }

    /// <summary>
    /// Day-labelled single-site data: one run per day collapses to a one-way
    /// design over days; several runs per day is the EP05 two-level nested
    /// design (balanced).
    /// </summary>
    private AnovaResult DayLabelled()
    {
        if (OneRunPerDay())
        {
            return OneWay(PrecisionDesign.DaysOnly, m => m.DayLabel!);
        }

        var days = _measurements.Select(m => m.DayLabel).Distinct().Count();
        if (days < MinimumRuns)
        {
            throw new DomainException("PR-010", $"At least {MinimumRuns} days are required for a days × runs design.");
        }

        return Nested(PrecisionDesign.DaysAndRuns, [Factor.Day, Factor.Run]);
    }

    /// <summary>
    /// EP05-A3 multi-site: the single-site layout repeated at every site, with
    /// sites as the top factor of a balanced nested ANOVA.
    /// </summary>
    private AnovaResult MultiSite(bool dayLabelled)
    {
        var sites = _measurements.Select(m => m.SiteLabel).Distinct().Count();
        if (sites < MinimumSites)
        {
            throw new DomainException("PR-018", $"A multi-site study needs at least {MinimumSites} sites.");
        }

        return !dayLabelled ? Nested(PrecisionDesign.SitesAndRuns, [Factor.Site, Factor.Run])
            : OneRunPerDay() ? Nested(PrecisionDesign.SitesAndDays, [Factor.Site, Factor.Day])
            : Nested(PrecisionDesign.SitesDaysAndRuns, [Factor.Site, Factor.Day, Factor.Run]);
    }

    private bool OneRunPerDay() =>
        _measurements
            .GroupBy(m => (m.SiteLabel, m.DayLabel))
            .All(d => d.Select(m => m.RunLabel).Distinct().Count() == 1);

    /// <summary>
    /// Balanced nested random-effects ANOVA over the given factor hierarchy
    /// (top first), replicates at the bottom. For factor i with cᵢ observations
    /// per node, SSᵢ = cᵢ·Σ(node mean − parent mean)², dfᵢ = nodesᵢ − nodesᵢ₋₁,
    /// and σ²ᵢ = (MSᵢ − MSᵢ₊₁)/cᵢ (MS of the level below; the error MS under
    /// the lowest factor), floored at 0. S_WL² sums every component except the
    /// site; S_R² adds the site. Satterthwaite df use the same linear
    /// combinations of mean squares, over the components that were not floored.
    /// </summary>
    private AnovaResult Nested(PrecisionDesign design, Factor[] factors)
    {
        var key = factors.Select<Factor, Func<PrecisionMeasurement, string>>(f => f switch
        {
            Factor.Site => m => m.SiteLabel!,
            Factor.Day => m => m.DayLabel!,
            _ => m => m.RunLabel,
        }).ToArray();

        // Group paths: every factor is nested in the ones above it.
        var groupsByDepth = new List<IReadOnlyList<IGrouping<string, PrecisionMeasurement>>>();
        for (var depth = 0; depth < factors.Length; depth++)
        {
            var d = depth;
            groupsByDepth.Add(_measurements
                .GroupBy(m => string.Join('\u001f', key.Take(d + 1).Select(k => k(m))))
                .ToList());
        }

        // Balance: every node at a depth holds the same number of observations.
        if (groupsByDepth.Any(level => level.Select(g => g.Count()).Distinct().Count() > 1))
        {
            throw new DomainException(
                "PR-014", "A nested design must be balanced: the same number of days and runs under every site and day, and the same number of replicates in every run.");
        }

        var leaves = groupsByDepth[^1];
        RequireMinimums(leaves.Count, leaves[0].Count());
        if (factors[0] == Factor.Site && groupsByDepth[1].Count / groupsByDepth[0].Count < MinimumRuns)
        {
            throw new DomainException("PR-010", $"Every site needs at least {MinimumRuns} days or runs.");
        }

        var n = _measurements.Count;
        var grand = _measurements.Average(m => (double)m.Value);
        var means = groupsByDepth
            .Select(level => level.ToDictionary(g => g.Key, g => g.Average(m => (double)m.Value)))
            .ToList();

        var ms = new double[factors.Length];
        var df = new double[factors.Length];
        var c = new double[factors.Length];
        for (var depth = 0; depth < factors.Length; depth++)
        {
            double ss = 0;
            foreach (var group in groupsByDepth[depth])
            {
                var parent = depth == 0 ? grand : means[depth - 1][ParentKey(group.Key)];
                ss += group.Count() * Math.Pow(means[depth][group.Key] - parent, 2);
            }

            c[depth] = (double)n / groupsByDepth[depth].Count;
            df[depth] = groupsByDepth[depth].Count - (depth == 0 ? 1 : groupsByDepth[depth - 1].Count);
            ms[depth] = ss / df[depth];
        }

        var ssError = leaves.Sum(g => g.Sum(m => Math.Pow((double)m.Value - means[^1][g.Key], 2)));
        var dfError = (double)(n - leaves.Count);
        var msError = ssError / dfError;

        var variance = new double[factors.Length];
        for (var depth = 0; depth < factors.Length; depth++)
        {
            var below = depth + 1 < factors.Length ? ms[depth + 1] : msError;
            variance[depth] = Math.Max(0, (ms[depth] - below) / c[depth]);
        }

        double? VarianceOf(Factor f) => Array.IndexOf(factors, f) is var i and >= 0 ? variance[i] : null;
        var withinLabDepths = Enumerable.Range(0, factors.Length).Where(i => factors[i] != Factor.Site).ToArray();
        var allDepths = Enumerable.Range(0, factors.Length).ToArray();

        var withinLabVariance = msError + withinLabDepths.Sum(i => variance[i]);
        var multiSite = factors[0] == Factor.Site;
        return new AnovaResult(
            design, grand, msError, dfError,
            VarianceOf(Factor.Run), VarianceOf(Factor.Day), VarianceOf(Factor.Site),
            withinLabVariance, Satterthwaite(Combination(withinLabDepths), dfError),
            multiSite ? withinLabVariance + variance[0] : null,
            multiSite ? Satterthwaite(Combination(allDepths), dfError) : null);

        // σ²ᵢ = (MSᵢ − MS_below)/cᵢ, so a sum of components plus σ²ₑ is a linear
        // combination of mean squares; floored components contribute nothing.
        IEnumerable<MeanSquareTerm> Combination(int[] depths)
        {
            var coefficient = new double[factors.Length + 1]; // last slot = error
            coefficient[^1] = 1;
            foreach (var i in depths.Where(i => variance[i] > 0))
            {
                coefficient[i] += 1 / c[i];
                coefficient[i + 1] -= 1 / c[i];
            }

            for (var i = 0; i < factors.Length; i++)
            {
                yield return new MeanSquareTerm(ms[i], df[i], coefficient[i]);
            }

            yield return new MeanSquareTerm(msError, dfError, coefficient[^1]);
        }
    }

    private static string ParentKey(string key) => key[..key.LastIndexOf('\u001f')];

    /// <summary>
    /// ν = (Σcᵢ·MSᵢ)² / Σ((cᵢ·MSᵢ)²/dfᵢ). Falls back to the repeatability df
    /// when the combined variance is zero (all replicates identical).
    /// </summary>
    private static double Satterthwaite(IEnumerable<MeanSquareTerm> terms, double fallbackDf)
    {
        var active = terms.Where(t => t.Coefficient != 0).ToList();
        var total = active.Sum(t => t.Coefficient * t.MeanSquare);
        var denominator = active.Sum(t => Math.Pow(t.Coefficient * t.MeanSquare, 2) / t.Df);
        return total > 0 && denominator > 0 ? total * total / denominator : fallbackDf;
    }

    /// <summary>95 % χ² interval for an SD, expressed as CV%: S·√(ν/χ²₀.₉₇₅) … S·√(ν/χ²₀.₀₂₅).</summary>
    private static (decimal? Lower, decimal? Upper) CvInterval(double sd, double mean, double df)
    {
        if (mean == 0 || sd == 0)
        {
            return (null, null);
        }

        var tail = (1 - ConfidenceLevel) / 2;
        var lower = sd * Math.Sqrt(df / ChiSquareDistribution.Quantile(1 - tail, df));
        var upper = sd * Math.Sqrt(df / ChiSquareDistribution.Quantile(tail, df));
        return (Cv(lower, mean), Cv(upper, mean));
    }

    /// <summary>EP15-A3 UVL = claim · √(χ²(1 − α/L, ν) / ν).</summary>
    private decimal? VerificationLimit(decimal? claimCvPct, double df) =>
        claimCvPct is { } claim
            ? Round((double)claim * Math.Sqrt(
                ChiSquareDistribution.Quantile(1 - VerificationAlpha / SeriesLevelCount, df) / df))
            : null;

    private static bool? Meets(decimal? observed, decimal? limit) =>
        observed is { } o && limit is { } l ? o <= l : null;

    private static (decimal? Sd, decimal? Cv) Component(double? variance, double mean) =>
        variance is { } v ? (Round(Math.Sqrt(v)), Cv(Math.Sqrt(v), mean)) : (null, null);

    private static void RequireMinimums(int groups, int smallestGroup)
    {
        if (groups < MinimumRuns)
        {
            throw new DomainException("PR-010", $"At least {MinimumRuns} runs are required.");
        }

        if (smallestGroup < MinimumReplicatesPerRun)
        {
            throw new DomainException("PR-011", $"Every run needs at least {MinimumReplicatesPerRun} replicates.");
        }
    }

    private int DistinctCount(Func<PrecisionMeasurement, string?> selector) =>
        _measurements.Select(selector).Where(v => v is not null).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? Cv(double sd, double mean) =>
        mean == 0 ? null : Round(sd / Math.Abs(mean) * 100.0);

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private static decimal RoundDf(double value) => Math.Round((decimal)value, 2);

    private void Invalidate()
    {
        Design = null;
        GrandMean = RepeatabilitySd = RepeatabilityCvPct = RepeatabilityDf = null;
        RepeatabilityCvLowerPct = RepeatabilityCvUpperPct = null;
        BetweenRunSd = BetweenRunCvPct = BetweenDaySd = BetweenDayCvPct = null;
        BetweenSiteSd = BetweenSiteCvPct = null;
        WithinLabSd = WithinLabCvPct = WithinLabDf = null;
        WithinLabCvLowerPct = WithinLabCvUpperPct = null;
        ReproducibilitySd = ReproducibilityCvPct = ReproducibilityDf = null;
        ReproducibilityCvLowerPct = ReproducibilityCvUpperPct = null;
        RepeatabilityVerificationLimitCvPct = WithinLabVerificationLimitCvPct = null;
        ReproducibilityVerificationLimitCvPct = null;
        MeetsRepeatabilityClaim = MeetsWithinLabClaim = MeetsReproducibilityClaim = null;
        WithinLabConsistentWithReproducibilityClaim = null;
        if (State == PrecisionState.Calculated)
        {
            State = PrecisionState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == PrecisionState.SignedOff)
        {
            throw new InvalidStateTransitionException("PR-013", "A signed-off study is immutable.");
        }
    }

    /// <summary>A factor of the nested hierarchy, top (site) to bottom (run).</summary>
    private enum Factor { Site, Day, Run }

    private sealed record MeanSquareTerm(double MeanSquare, double Df, double Coefficient);

    private sealed record AnovaResult(
        PrecisionDesign Design, double GrandMean,
        double RepeatabilityVariance, double RepeatabilityDf,
        double? BetweenRunVariance, double? BetweenDayVariance, double? BetweenSiteVariance,
        double WithinLabVariance, double WithinLabDf,
        double? ReproducibilityVariance, double? ReproducibilityDf);
}

public sealed record PrecisionStudySignedOff(
    Guid StudyId, string StudyRef, string Analyte, Guid TenantId) : DomainEvent;
