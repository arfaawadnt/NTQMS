using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum PrecisionState { DataEntry, Calculated, SignedOff }

/// <summary>One replicate belonging to a named run (a run groups its replicates).</summary>
public sealed class PrecisionMeasurement : Entity
{
    internal PrecisionMeasurement(string runLabel, decimal value)
    {
        RunLabel = runLabel;
        Value = value;
    }

    private PrecisionMeasurement() { RunLabel = null!; }

    public string RunLabel { get; private set; }
    public decimal Value { get; private set; }
}

/// <summary>Component summary for a run — mean and replicate count, for reporting.</summary>
public sealed record PrecisionRunSummary(string RunLabel, int ReplicateCount, decimal Mean);

/// <summary>
/// Imprecision study (CLSI EP05, two-level nested design at one concentration):
/// replicates grouped into runs. Calculate applies a one-way random-effects
/// ANOVA to separate repeatability (within-run) from the between-run component
/// and combine them into the within-laboratory (total) imprecision, reporting
/// each as an SD and a CV relative to the grand mean. Optional manufacturer
/// claims (CV%) are verified per component. Measurements are immutable
/// evidence; statistics are derivable-only; sign-off freezes the study.
/// </summary>
public sealed class PrecisionStudy : AggregateRoot, ITenantScoped
{
    public const int MinimumRuns = 2;
    public const int MinimumReplicatesPerRun = 2;

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
    /// <summary>Optional manufacturer repeatability claim (within-run CV%).</summary>
    public decimal? ClaimedRepeatabilityCvPct { get; private set; }
    /// <summary>Optional manufacturer within-laboratory claim (total CV%).</summary>
    public decimal? ClaimedWithinLabCvPct { get; private set; }
    public PrecisionState State { get; private set; }

    // Derived (null until Calculate).
    public decimal? GrandMean { get; private set; }
    public decimal? RepeatabilitySd { get; private set; }
    public decimal? RepeatabilityCvPct { get; private set; }
    public decimal? BetweenRunSd { get; private set; }
    public decimal? BetweenRunCvPct { get; private set; }
    public decimal? WithinLabSd { get; private set; }
    public decimal? WithinLabCvPct { get; private set; }
    public bool? MeetsRepeatabilityClaim { get; private set; }
    public bool? MeetsWithinLabClaim { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<PrecisionMeasurement> Measurements => _measurements.AsReadOnly();

    public static PrecisionStudy Configure(
        string studyRef, string analyte, string unit, string level,
        decimal? claimedRepeatabilityCvPct, decimal? claimedWithinLabCvPct)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("PR-001", "An analyte is required.");
        }

        if (claimedRepeatabilityCvPct is <= 0m || claimedWithinLabCvPct is <= 0m)
        {
            throw new DomainException("PR-002", "Claimed CVs, when given, must be positive percentages.");
        }

        return new PrecisionStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            Level = string.IsNullOrWhiteSpace(level) ? string.Empty : level.Trim(),
            ClaimedRepeatabilityCvPct = claimedRepeatabilityCvPct,
            ClaimedWithinLabCvPct = claimedWithinLabCvPct,
            State = PrecisionState.DataEntry,
        };
    }

    public Guid AddMeasurement(string runLabel, decimal value)
    {
        RequireEditable();
        if (string.IsNullOrWhiteSpace(runLabel))
        {
            throw new DomainException("PR-003", "A run label is required — replicates are grouped by run.");
        }

        var measurement = new PrecisionMeasurement(runLabel.Trim(), value);
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
    /// One-way random-effects ANOVA. Runs are the random factor; MSW estimates
    /// the repeatability variance and (MSB − MSW)/n₀ the between-run component
    /// (floored at 0). Within-lab variance is their sum.
    /// </summary>
    public void Calculate()
    {
        RequireEditable();
        var runs = _measurements
            .GroupBy(m => m.RunLabel)
            .Select(g => g.Select(m => (double)m.Value).ToArray())
            .ToList();

        if (runs.Count < MinimumRuns)
        {
            throw new DomainException("PR-010", $"At least {MinimumRuns} runs are required.");
        }

        if (runs.Any(r => r.Length < MinimumReplicatesPerRun))
        {
            throw new DomainException("PR-011", $"Every run needs at least {MinimumReplicatesPerRun} replicates.");
        }

        var k = runs.Count;
        var n = _measurements.Count;
        var grand = runs.SelectMany(r => r).Average();

        double ssWithin = 0, ssBetween = 0;
        foreach (var run in runs)
        {
            var runMean = run.Average();
            ssWithin += run.Sum(v => (v - runMean) * (v - runMean));
            ssBetween += run.Length * (runMean - grand) * (runMean - grand);
        }

        var dfWithin = n - k;
        var dfBetween = k - 1;
        var msWithin = ssWithin / dfWithin; // repeatability variance
        var msBetween = ssBetween / dfBetween;

        // n₀ = (N − Σnᵢ²/N)/(k−1); equals the common replicate count for a balanced design.
        var sumNiSquared = runs.Sum(r => (double)r.Length * r.Length);
        var n0 = (n - sumNiSquared / n) / dfBetween;
        var betweenVar = Math.Max(0, (msBetween - msWithin) / n0);

        var repeatabilitySd = Math.Sqrt(msWithin);
        var betweenRunSd = Math.Sqrt(betweenVar);
        var withinLabSd = Math.Sqrt(msWithin + betweenVar);

        GrandMean = Round(grand);
        RepeatabilitySd = Round(repeatabilitySd);
        RepeatabilityCvPct = Cv(repeatabilitySd, grand);
        BetweenRunSd = Round(betweenRunSd);
        BetweenRunCvPct = Cv(betweenRunSd, grand);
        WithinLabSd = Round(withinLabSd);
        WithinLabCvPct = Cv(withinLabSd, grand);

        MeetsRepeatabilityClaim = ClaimedRepeatabilityCvPct is { } rc ? RepeatabilityCvPct <= rc : null;
        MeetsWithinLabClaim = ClaimedWithinLabCvPct is { } wc ? WithinLabCvPct <= wc : null;

        State = PrecisionState.Calculated;
    }

    /// <summary>Per-run means for the workspace table/plot — derived, never stored.</summary>
    public IReadOnlyList<PrecisionRunSummary> RunSummaries() =>
        _measurements
            .GroupBy(m => m.RunLabel)
            .OrderBy(g => g.Key)
            .Select(g => new PrecisionRunSummary(g.Key, g.Count(), Math.Round(g.Average(m => m.Value), 4)))
            .ToList();

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

    private static decimal? Cv(double sd, double mean) =>
        mean == 0 ? null : Round(sd / mean * 100.0);

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private void Invalidate()
    {
        GrandMean = RepeatabilitySd = RepeatabilityCvPct = null;
        BetweenRunSd = BetweenRunCvPct = null;
        WithinLabSd = WithinLabCvPct = null;
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
}

public sealed record PrecisionStudySignedOff(
    Guid StudyId, string StudyRef, string Analyte, Guid TenantId) : DomainEvent;
