using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum ReferenceIntervalState { DataEntry, Calculated, SignedOff }

public enum ReferenceIntervalVerdict { Verified, Rejected }

/// <summary>One reference-individual sample used to verify a claimed interval.</summary>
public sealed class ReferenceSample : Entity
{
    internal ReferenceSample(decimal value, string? subjectRef)
    {
        Value = value;
        SubjectRef = subjectRef;
    }

    private ReferenceSample() { }

    public decimal Value { get; private set; }
    public string? SubjectRef { get; private set; }
}

/// <summary>
/// Reference-interval verification / transference study (CLSI EP28-A3c small-N
/// verification): a laboratory verifies a claimed interval [lower, upper] by
/// testing a modest set of reference individuals (the guideline uses 20). The
/// interval is verified when no more than the allowed number of samples fall
/// OUTSIDE it — the binomial rule permits ≤10% outside (2 of 20). Exceeding the
/// allowance rejects the transference: the lab must widen the panel or
/// establish its own interval. Samples are immutable evidence; the verdict is
/// derivable-only; sign-off freezes the study.
/// </summary>
public sealed class ReferenceIntervalStudy : AggregateRoot, ITenantScoped
{
    /// <summary>EP28 small-N verification panel size.</summary>
    public const int RecommendedSampleCount = 20;

    /// <summary>Proportion permitted outside the interval before transference is rejected (10%).</summary>
    public const decimal AllowedOutsideFraction = 0.10m;

    private readonly List<ReferenceSample> _samples = [];

    private ReferenceIntervalStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
        Population = null!;
        Source = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    /// <summary>The population the interval applies to, e.g. "Adult female".</summary>
    public string Population { get; private set; }
    /// <summary>Where the claimed interval came from (manufacturer insert, literature, prior lab…).</summary>
    public string Source { get; private set; }
    public decimal ClaimedLower { get; private set; }
    public decimal ClaimedUpper { get; private set; }
    public ReferenceIntervalState State { get; private set; }

    // Derived (null until Calculate).
    public int? SampleCount { get; private set; }
    public int? OutsideCount { get; private set; }
    /// <summary>Max samples allowed outside for this panel size (floor of 10%).</summary>
    public int? AllowedOutside { get; private set; }
    public ReferenceIntervalVerdict? Verdict { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<ReferenceSample> Samples => _samples.AsReadOnly();

    public static ReferenceIntervalStudy Configure(
        string studyRef, string analyte, string unit, string population, string source,
        decimal claimedLower, decimal claimedUpper)
    {
        if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(population))
        {
            throw new DomainException("RI-001", "An analyte and the reference population are required.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new DomainException("RI-002", "The source of the claimed interval is required for traceability.");
        }

        if (claimedUpper <= claimedLower)
        {
            throw new DomainException("RI-003", "The claimed upper limit must exceed the lower limit.");
        }

        return new ReferenceIntervalStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            Population = population.Trim(),
            Source = source.Trim(),
            ClaimedLower = claimedLower,
            ClaimedUpper = claimedUpper,
            State = ReferenceIntervalState.DataEntry,
        };
    }

    public Guid AddSample(decimal value, string? subjectRef)
    {
        RequireEditable();
        var sample = new ReferenceSample(value, string.IsNullOrWhiteSpace(subjectRef) ? null : subjectRef.Trim());
        _samples.Add(sample);
        Invalidate();
        return sample.Id;
    }

    public void RemoveSample(Guid sampleId)
    {
        RequireEditable();
        var sample = _samples.FirstOrDefault(s => s.Id == sampleId)
            ?? throw new DomainException("RI-404", "Reference sample not found.");
        _samples.Remove(sample);
        Invalidate();
    }

    public void Calculate()
    {
        RequireEditable();
        if (_samples.Count < RecommendedSampleCount)
        {
            throw new DomainException("RI-010",
                $"At least {RecommendedSampleCount} reference samples are required to verify a claimed interval (EP28-A3c).");
        }

        var outside = _samples.Count(s => s.Value < ClaimedLower || s.Value > ClaimedUpper);
        var allowed = (int)Math.Floor(_samples.Count * AllowedOutsideFraction);

        SampleCount = _samples.Count;
        OutsideCount = outside;
        AllowedOutside = allowed;
        Verdict = outside <= allowed ? ReferenceIntervalVerdict.Verified : ReferenceIntervalVerdict.Rejected;
        State = ReferenceIntervalState.Calculated;
    }

    /// <summary>Whether a sample lies outside the claimed interval — for reporting/plotting.</summary>
    public bool IsOutside(ReferenceSample sample) =>
        sample.Value < ClaimedLower || sample.Value > ClaimedUpper;

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        if (State != ReferenceIntervalState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "RI-011", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = ReferenceIntervalState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new ReferenceIntervalSignedOff(Id, StudyRef, Analyte, Verdict!.Value, TenantId));
    }

    private void Invalidate()
    {
        SampleCount = OutsideCount = AllowedOutside = null;
        Verdict = null;
        if (State == ReferenceIntervalState.Calculated)
        {
            State = ReferenceIntervalState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == ReferenceIntervalState.SignedOff)
        {
            throw new InvalidStateTransitionException("RI-012", "A signed-off study is immutable.");
        }
    }
}

public sealed record ReferenceIntervalSignedOff(
    Guid StudyId, string StudyRef, string Analyte, ReferenceIntervalVerdict Verdict, Guid TenantId) : DomainEvent;
