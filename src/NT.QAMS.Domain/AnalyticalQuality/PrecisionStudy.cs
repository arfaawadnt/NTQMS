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
/// </list>
/// </summary>
public enum PrecisionDesign { RunsOnly, DaysOnly, DaysAndRuns }

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

/// <summary>One replicate belonging to a named run (and optionally a named day).</summary>
public sealed class PrecisionMeasurement : Entity
{
    internal PrecisionMeasurement(string runLabel, decimal value, string? dayLabel, PrecisionConditions conditions)
    {
        RunLabel = runLabel;
        Value = value;
        DayLabel = dayLabel;
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
    public string? Operator { get; private set; }
    public string? Instrument { get; private set; }
    public string? ReagentLot { get; private set; }
    public string? CalibratorLot { get; private set; }
    public DateOnly? MeasuredOn { get; private set; }
}

/// <summary>Component summary for a run — mean and replicate count, for reporting.</summary>
public sealed record PrecisionRunSummary(string? DayLabel, string RunLabel, int ReplicateCount, decimal Mean);

/// <summary>
/// How many distinct levels of each intermediate-precision factor the data
/// spans — derived from the replicates, never stored.
/// </summary>
public sealed record PrecisionConditionsCoverage(
    int Days, int Runs, int Operators, int Instruments, int ReagentLots, int CalibratorLots,
    DateOnly? FirstMeasuredOn, DateOnly? LastMeasuredOn, bool MeetsEp15MinimumDesign);

/// <summary>
/// Imprecision study (CLSI EP05-A3 establishment / EP15-A3 verification) at one
/// concentration level. Replicates are grouped into runs, and optionally runs
/// into days; Calculate applies the matching random-effects ANOVA (see
/// <see cref="PrecisionDesign"/>) to separate repeatability (within-run) from
/// the between-run and between-day components and combine them into the
/// within-laboratory imprecision (intermediate precision, S_WL / S_Rw). Each SD
/// is reported with its CV, its degrees of freedom (Satterthwaite for S_WL) and
/// a 95 % χ² confidence interval. A manufacturer claim is verified the EP15-A3
/// way: the claim is accepted when the observed CV does not exceed the upper
/// verification limit UVL = claim · √(χ²(1 − α/L, ν) / ν), α = 5 % split across
/// the L levels of the study series. Between-LABORATORY reproducibility is out
/// of scope by design — it comes from the manufacturer's multi-site study or
/// PT/EQA, not from one laboratory's data. Measurements are immutable evidence;
/// statistics are derivable-only; sign-off freezes the study.
/// </summary>
public sealed class PrecisionStudy : AggregateRoot, ITenantScoped
{
    public const int MinimumRuns = 2;
    public const int MinimumReplicatesPerRun = 2;
    /// <summary>Family-wise false-rejection rate of a claim verification (EP15-A3).</summary>
    public const double VerificationAlpha = 0.05;
    /// <summary>Two-sided confidence level of the reported SD/CV intervals.</summary>
    public const double ConfidenceLevel = 0.95;
    /// <summary>EP15-A3 minimum: 5 days (or runs) …</summary>
    public const int Ep15MinimumGroups = 5;
    /// <summary>… and 25 results in total.</summary>
    public const int Ep15MinimumResults = 25;
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
    public PrecisionState State { get; private set; }

    // Derived (null until Calculate).
    public PrecisionDesign? Design { get; private set; }
    public decimal? GrandMean { get; private set; }
    public decimal? RepeatabilitySd { get; private set; }
    public decimal? RepeatabilityCvPct { get; private set; }
    public decimal? RepeatabilityDf { get; private set; }
    public decimal? RepeatabilityCvLowerPct { get; private set; }
    public decimal? RepeatabilityCvUpperPct { get; private set; }
    /// <summary>Between-run component; null in a days-only design (one run per day cannot separate it).</summary>
    public decimal? BetweenRunSd { get; private set; }
    public decimal? BetweenRunCvPct { get; private set; }
    /// <summary>Between-day component; null in a runs-only design.</summary>
    public decimal? BetweenDaySd { get; private set; }
    public decimal? BetweenDayCvPct { get; private set; }
    public decimal? WithinLabSd { get; private set; }
    public decimal? WithinLabCvPct { get; private set; }
    /// <summary>Satterthwaite effective degrees of freedom of S_WL.</summary>
    public decimal? WithinLabDf { get; private set; }
    public decimal? WithinLabCvLowerPct { get; private set; }
    public decimal? WithinLabCvUpperPct { get; private set; }
    /// <summary>EP15-A3 upper verification limit for the repeatability claim (CV%).</summary>
    public decimal? RepeatabilityVerificationLimitCvPct { get; private set; }
    /// <summary>EP15-A3 upper verification limit for the within-lab claim (CV%).</summary>
    public decimal? WithinLabVerificationLimitCvPct { get; private set; }
    /// <summary>Observed repeatability CV ≤ its UVL (null without a claim).</summary>
    public bool? MeetsRepeatabilityClaim { get; private set; }
    /// <summary>Observed within-lab CV ≤ its UVL (null without a claim).</summary>
    public bool? MeetsWithinLabClaim { get; private set; }

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
        string? seriesRef, int seriesLevelCount)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("PR-001", "An analyte is required.");
        }

        if (claimedRepeatabilityCvPct is <= 0m || claimedWithinLabCvPct is <= 0m)
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
            State = PrecisionState.DataEntry,
        };
    }

    /// <summary>Adds a replicate with no day label and no recorded conditions.</summary>
    public Guid AddMeasurement(string runLabel, decimal value) =>
        AddMeasurement(runLabel, value, null, PrecisionConditions.None);

    /// <summary>
    /// Adds a replicate. A day label nests the run within that day — run "R1"
    /// on day "D01" and run "R1" on day "D02" are different runs.
    /// </summary>
    public Guid AddMeasurement(string runLabel, decimal value, string? dayLabel, PrecisionConditions conditions)
    {
        RequireEditable();
        if (string.IsNullOrWhiteSpace(runLabel))
        {
            throw new DomainException("PR-003", "A run label is required — replicates are grouped by run.");
        }

        var measurement = new PrecisionMeasurement(
            runLabel.Trim(), value, Clean(dayLabel),
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
        var labelled = _measurements.Count(m => m.DayLabel is not null);
        if (labelled > 0 && labelled < _measurements.Count)
        {
            throw new DomainException(
                "PR-015", "Either every replicate carries a day label or none does — a design cannot be partly nested.");
        }

        var result = labelled == 0 ? OneWay(PrecisionDesign.RunsOnly, m => m.RunLabel) : DayLabelled();

        var grand = result.GrandMean;
        Design = result.Design;
        GrandMean = Round(grand);

        var repeatabilitySd = Math.Sqrt(result.RepeatabilityVariance);
        RepeatabilitySd = Round(repeatabilitySd);
        RepeatabilityCvPct = Cv(repeatabilitySd, grand);
        RepeatabilityDf = RoundDf(result.RepeatabilityDf);

        BetweenRunSd = result.BetweenRunVariance is { } run ? Round(Math.Sqrt(run)) : null;
        BetweenRunCvPct = result.BetweenRunVariance is { } runV ? Cv(Math.Sqrt(runV), grand) : null;
        BetweenDaySd = result.BetweenDayVariance is { } day ? Round(Math.Sqrt(day)) : null;
        BetweenDayCvPct = result.BetweenDayVariance is { } dayV ? Cv(Math.Sqrt(dayV), grand) : null;

        var withinLabSd = Math.Sqrt(result.WithinLabVariance);
        WithinLabSd = Round(withinLabSd);
        WithinLabCvPct = Cv(withinLabSd, grand);
        WithinLabDf = RoundDf(result.WithinLabDf);

        (RepeatabilityCvLowerPct, RepeatabilityCvUpperPct) = CvInterval(repeatabilitySd, grand, result.RepeatabilityDf);
        (WithinLabCvLowerPct, WithinLabCvUpperPct) = CvInterval(withinLabSd, grand, result.WithinLabDf);

        RepeatabilityVerificationLimitCvPct = VerificationLimit(ClaimedRepeatabilityCvPct, result.RepeatabilityDf);
        WithinLabVerificationLimitCvPct = VerificationLimit(ClaimedWithinLabCvPct, result.WithinLabDf);
        MeetsRepeatabilityClaim = RepeatabilityVerificationLimitCvPct is { } rl && RepeatabilityCvPct is { } rcv
            ? rcv <= rl : null;
        MeetsWithinLabClaim = WithinLabVerificationLimitCvPct is { } wl && WithinLabCvPct is { } wcv
            ? wcv <= wl : null;

        State = PrecisionState.Calculated;
    }

    /// <summary>Per-run means for the workspace table/plot — derived, never stored.</summary>
    public IReadOnlyList<PrecisionRunSummary> RunSummaries() =>
        _measurements
            .GroupBy(m => (m.DayLabel, m.RunLabel))
            .OrderBy(g => g.Key.DayLabel, StringComparer.Ordinal)
            .ThenBy(g => g.Key.RunLabel, StringComparer.Ordinal)
            .Select(g => new PrecisionRunSummary(
                g.Key.DayLabel, g.Key.RunLabel, g.Count(), Math.Round(g.Average(m => m.Value), 4)))
            .ToList();

    /// <summary>Which intermediate-precision factors the replicates span — derived, never stored.</summary>
    public PrecisionConditionsCoverage ConditionsCoverage()
    {
        var days = _measurements.Where(m => m.DayLabel is not null).Select(m => m.DayLabel).Distinct().Count();
        var runs = _measurements.Select(m => (m.DayLabel, m.RunLabel)).Distinct().Count();
        var dates = _measurements.Where(m => m.MeasuredOn is not null).Select(m => m.MeasuredOn!.Value).ToList();
        var groups = days > 0 ? days : runs;
        return new PrecisionConditionsCoverage(
            days, runs,
            DistinctCount(m => m.Operator), DistinctCount(m => m.Instrument),
            DistinctCount(m => m.ReagentLot), DistinctCount(m => m.CalibratorLot),
            dates.Count > 0 ? dates.Min() : null, dates.Count > 0 ? dates.Max() : null,
            groups >= Ep15MinimumGroups && _measurements.Count >= Ep15MinimumResults);
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
    /// with Satterthwaite df over the two mean squares.
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
            msWithin + betweenVar, Satterthwaite(terms, dfWithin));
    }

    /// <summary>
    /// Day-labelled data: one run per day collapses to a one-way design over
    /// days; several runs per day is the EP05 two-level nested design, which
    /// must be balanced (equal runs per day, equal replicates per run).
    /// </summary>
    private AnovaResult DayLabelled()
    {
        var days = _measurements
            .GroupBy(m => m.DayLabel!)
            .Select(d => d.GroupBy(m => m.RunLabel).Select(r => r.Select(m => (double)m.Value).ToArray()).ToList())
            .ToList();

        if (days.All(d => d.Count == 1))
        {
            return OneWay(PrecisionDesign.DaysOnly, m => m.DayLabel!);
        }

        var runsPerDay = days[0].Count;
        var repsPerRun = days[0][0].Length;
        if (days.Any(d => d.Count != runsPerDay || d.Any(r => r.Length != repsPerRun)))
        {
            throw new DomainException(
                "PR-014", "A days × runs design must be balanced: the same number of runs every day and the same number of replicates in every run.");
        }

        if (days.Count < MinimumRuns)
        {
            throw new DomainException("PR-010", $"At least {MinimumRuns} days are required for a days × runs design.");
        }

        RequireMinimums(days.Count * runsPerDay, repsPerRun);

        double d = days.Count, r = runsPerDay, n = repsPerRun;
        var grand = days.SelectMany(x => x).SelectMany(x => x).Average();

        double ssDay = 0, ssRun = 0, ssError = 0;
        foreach (var day in days)
        {
            var dayMean = day.SelectMany(x => x).Average();
            ssDay += r * n * (dayMean - grand) * (dayMean - grand);
            foreach (var run in day)
            {
                var runMean = run.Average();
                ssRun += n * (runMean - dayMean) * (runMean - dayMean);
                ssError += run.Sum(v => (v - runMean) * (v - runMean));
            }
        }

        double dfDay = d - 1, dfRun = d * (r - 1), dfError = d * r * (n - 1);
        double msDay = ssDay / dfDay, msRun = ssRun / dfRun, msError = ssError / dfError;

        var runVar = Math.Max(0, (msRun - msError) / n);
        var dayVar = Math.Max(0, (msDay - msRun) / (r * n));

        // S_WL² as a linear combination of mean squares, only over the
        // components that were not floored, for the Satterthwaite df.
        double cError = 1, cRun = 0, cDay = 0;
        if (runVar > 0)
        {
            cRun += 1 / n;
            cError -= 1 / n;
        }

        if (dayVar > 0)
        {
            cDay += 1 / (r * n);
            cRun -= 1 / (r * n);
        }

        var terms = new List<MeanSquareTerm>
        {
            new(msError, dfError, cError), new(msRun, dfRun, cRun), new(msDay, dfDay, cDay),
        };

        return new AnovaResult(
            PrecisionDesign.DaysAndRuns, grand, msError, dfError, runVar, dayVar,
            msError + runVar + dayVar, Satterthwaite(terms, dfError));
    }

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
        WithinLabSd = WithinLabCvPct = WithinLabDf = null;
        WithinLabCvLowerPct = WithinLabCvUpperPct = null;
        RepeatabilityVerificationLimitCvPct = WithinLabVerificationLimitCvPct = null;
        MeetsRepeatabilityClaim = MeetsWithinLabClaim = null;
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

    private sealed record MeanSquareTerm(double MeanSquare, double Df, double Coefficient);

    private sealed record AnovaResult(
        PrecisionDesign Design, double GrandMean,
        double RepeatabilityVariance, double RepeatabilityDf,
        double? BetweenRunVariance, double? BetweenDayVariance,
        double WithinLabVariance, double WithinLabDf);
}

public sealed record PrecisionStudySignedOff(
    Guid StudyId, string StudyRef, string Analyte, Guid TenantId) : DomainEvent;
