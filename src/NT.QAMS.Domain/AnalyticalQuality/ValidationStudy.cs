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
    /// Computes precision (CV%) and, where reference values exist, mean bias%,
    /// then judges against TEa. Derivable-only: callable any time there is data.
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

        var measured = _replicates.Select(r => r.Measured).ToList();
        var mean = measured.Average();
        if (mean == 0m)
        {
            throw new DomainException("MV-014", "Cannot compute CV against a zero mean.");
        }

        var variance = measured.Sum(m => (m - mean) * (m - mean)) / (measured.Count - 1);
        var sd = (decimal)Math.Sqrt((double)variance);
        Cv = Math.Round(sd / Math.Abs(mean) * 100m, 3);

        var withReference = _replicates.Where(r => r.Reference is > 0m).ToList();
        if (withReference.Count > 0)
        {
            var biases = withReference.Select(r => (r.Measured - r.Reference!.Value) / r.Reference!.Value * 100m);
            MeanBias = Math.Round(biases.Average(), 3);
        }

        // Total error estimate: |bias%| + 1.65 * CV% (one-sided 95%) judged vs TEa%.
        var totalError = (Math.Abs(MeanBias ?? 0m)) + 1.65m * Cv.Value;
        Passed = totalError <= TotalAllowableError;

        State = ValidationState.StatsCalculated;
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
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
