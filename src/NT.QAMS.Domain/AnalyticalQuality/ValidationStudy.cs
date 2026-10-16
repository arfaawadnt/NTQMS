using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum ValidationState { ProtocolConfigured, DataEntered, StatsCalculated, SignedOff }

public sealed class ValidationReplicate : Entity
{
    internal ValidationReplicate(string level, decimal measured, decimal? reference)
    {
        Level = level;
        Measured = measured;
        Reference = reference;
    }

    private ValidationReplicate() { Level = null!; }

    public string Level { get; private set; }
    public decimal Measured { get; private set; }
    public decimal? Reference { get; private set; }
}

/// <summary>
/// A CLSI method-validation study. Lifecycle: ProtocolConfigured → DataEntered →
/// StatsCalculated → SignedOff (locked). Replicates are immutable evidence
/// (void-and-re-enter, never edit-in-place). Statistics are derivable-only — they
/// are recomputed from replicates, never hand-set — and sign-off freezes the study.
/// </summary>
public sealed class ValidationStudy : AggregateRoot, ITenantScoped
{
    /// <summary>One-sided 95 % normal quantile used in the total-error estimate.</summary>
    public const decimal TotalErrorZ = 1.65m;

    private readonly List<ValidationReplicate> _replicates = [];

    private ValidationStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Protocol = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Protocol { get; private set; }
    public decimal TotalAllowableError { get; private set; }
    public ValidationState State { get; private set; }
    public decimal? MeanBias { get; private set; }
    public decimal? Cv { get; private set; }
    public bool? Passed { get; private set; }
    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<ValidationReplicate> Replicates => _replicates.AsReadOnly();

    public static ValidationStudy Configure(
        string studyRef, string analyte, string protocol, decimal totalAllowableError)
    {
        if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(protocol))
        {
            throw new DomainException("MV-001", "Analyte and CLSI protocol are required.");
        }

        if (totalAllowableError <= 0m)
        {
            throw new DomainException("MV-002", "Total allowable error (TEa) must be positive.");
        }

        return new ValidationStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Protocol = protocol.Trim().ToUpperInvariant(),
            TotalAllowableError = totalAllowableError,
            State = ValidationState.ProtocolConfigured,
        };
    }

    public void EnterReplicate(string level, decimal measured, decimal? reference)
    {
        if (State is ValidationState.SignedOff)
        {
            throw new InvalidStateTransitionException("MV-010", "A signed-off study is immutable.");
        }

        if (State == ValidationState.StatsCalculated)
        {
            // Reopening for more data voids the prior results (they must be recomputed).
            MeanBias = null;
            Cv = null;
            Passed = null;
        }

        if (string.IsNullOrWhiteSpace(level))
        {
            throw new DomainException("MV-011", "A replicate level (e.g. Low/Mid/High) is required.");
        }

        _replicates.Add(new ValidationReplicate(level.Trim(), measured, reference));
        State = ValidationState.DataEntered;
    }

    /// <summary>
    /// Computes precision and bias PER LEVEL — imprecision is
    /// concentration-dependent, so pooling Low/Mid/High into one SD would count
    /// the spread BETWEEN levels as imprecision (defect V-D1). Each level's
    /// total error |bias%| + 1.65·CV% is judged against TEa and the study passes
    /// only when every level does. The stored CV is the df-weighted pooled
    /// within-level CV √(Σ(nᵢ−1)·CVᵢ² / Σ(nᵢ−1)); the stored mean bias averages
    /// every replicate that has a reference. Derivable-only: callable any time
    /// there is data.
    /// </summary>
    public void CalculateStatistics()
    {
        if (State is not (ValidationState.DataEntered or ValidationState.StatsCalculated))
        {
            throw new InvalidStateTransitionException("MV-012", $"Cannot calculate statistics in state {State}.");
        }

        if (_replicates.Count < 2)
        {
            throw new DomainException("MV-013", "At least two replicates are required to compute statistics.");
        }

        var levels = ComputeLevels(strict: true);
        var pooledDf = levels.Sum(l => l.Replicates - 1);
        var pooledCvSquared = levels.Sum(l => (l.Replicates - 1) * l.Cv * l.Cv) / pooledDf;
        Cv = Math.Round((decimal)Math.Sqrt((double)pooledCvSquared), 3);

        var withReference = _replicates.Where(r => r.Reference is > 0m).ToList();
        MeanBias = withReference.Count > 0
            ? Math.Round(withReference.Average(r => (r.Measured - r.Reference!.Value) / r.Reference!.Value * 100m), 3)
            : null;

        Passed = levels.All(l => l.Passes);
        State = ValidationState.StatsCalculated;
    }

    /// <summary>
    /// Per-level statistics for display — derived from the replicates, never
    /// stored. Levels that cannot yet be computed (fewer than two replicates,
    /// zero mean) are omitted rather than failing the read.
    /// </summary>
    public IReadOnlyList<ValidationLevelResult> LevelResults() => ComputeLevels(strict: false);

    /// <summary>
    /// Every level needs at least two replicates and a non-zero mean; when
    /// <paramref name="strict"/> an ineligible level is an error, otherwise it is skipped.
    /// </summary>
    private List<ValidationLevelResult> ComputeLevels(bool strict)
    {
        var results = new List<ValidationLevelResult>();
        foreach (var level in _replicates
                     .GroupBy(r => r.Level, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var measured = level.Select(r => r.Measured).ToList();
            var mean = measured.Average();
            if (measured.Count < 2 || mean == 0m)
            {
                if (!strict)
                {
                    continue;
                }

                throw measured.Count < 2
                    ? new DomainException(
                        "MV-016", $"Level '{level.Key}' needs at least two replicates — precision is computed per level.")
                    : new DomainException("MV-014", $"Cannot compute CV against a zero mean (level '{level.Key}').");
            }

            var variance = measured.Sum(m => (m - mean) * (m - mean)) / (measured.Count - 1);
            var sd = (decimal)Math.Sqrt((double)variance);
            var cv = Math.Round(sd / Math.Abs(mean) * 100m, 3);

            var referenced = level.Where(r => r.Reference is > 0m).ToList();
            decimal? bias = referenced.Count > 0
                ? Math.Round(referenced.Average(r => (r.Measured - r.Reference!.Value) / r.Reference!.Value * 100m), 3)
                : null;

            // Total error estimate: |bias%| + 1.65 · CV% (one-sided 95 %) judged vs TEa%.
            var totalError = Math.Round(Math.Abs(bias ?? 0m) + TotalErrorZ * cv, 3);
            results.Add(new ValidationLevelResult(
                level.Key, measured.Count, Math.Round(mean, 4), Math.Round(sd, 4), cv, bias, totalError,
                totalError <= TotalAllowableError));
        }

        return results;
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001");
        if (State != ValidationState.StatsCalculated)
        {
            throw new InvalidStateTransitionException("MV-015", "Statistics must be calculated before sign-off.");
        }

        State = ValidationState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new ValidationStudySignedOff(Id, StudyRef, Analyte, Passed ?? false, actorId, TenantId));
    }
}

public sealed record ValidationStudySignedOff(
    Guid StudyId, string StudyRef, string Analyte, bool Passed, Guid SignedOffBy, Guid TenantId) : DomainEvent;

/// <summary>One level's precision, bias and total-error verdict within a validation study.</summary>
public sealed record ValidationLevelResult(
    string Level, int Replicates, decimal Mean, decimal Sd, decimal Cv, decimal? Bias,
    decimal TotalError, bool Passes);
