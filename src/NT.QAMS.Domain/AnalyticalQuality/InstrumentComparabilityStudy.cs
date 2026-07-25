using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum InstrumentComparabilityState { DataEntry, Calculated, SignedOff }

/// <summary>One instrument's reading of a shared sample.</summary>
public sealed class InstrumentReading : Entity
{
    internal InstrumentReading(string instrument, string sampleId, decimal value)
    {
        Instrument = instrument;
        SampleId = sampleId;
        Value = value;
    }

    private InstrumentReading() { Instrument = null!; SampleId = null!; }

    public string Instrument { get; private set; }
    /// <summary>Identifies the shared sample so instruments are compared like-for-like.</summary>
    public string SampleId { get; private set; }
    public decimal Value { get; private set; }
}

/// <summary>Per-instrument result: mean % bias against the reference on shared samples.</summary>
public sealed record InstrumentResult(
    string Instrument, int PairedSamples, decimal MeanBiasPct, bool Comparable);

/// <summary>
/// Instrument-to-instrument comparability: shared samples measured across a
/// fleet, each instrument compared to a designated reference on the samples they
/// both ran. The mean percentage bias per instrument is assessed against the
/// allowable limit; within it, the instrument is comparable to the reference.
/// Readings are immutable; results are derivable-only; sign-off freezes the study.
/// </summary>
public sealed class InstrumentComparabilityStudy : AggregateRoot, ITenantScoped
{
    private readonly List<InstrumentReading> _readings = [];

    private InstrumentComparabilityStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
        ReferenceInstrument = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    /// <summary>The instrument every other is compared against.</summary>
    public string ReferenceInstrument { get; private set; }
    /// <summary>Maximum acceptable mean bias vs the reference, as a percentage.</summary>
    public decimal AllowableBiasPct { get; private set; }
    public InstrumentComparabilityState State { get; private set; }

    // Derived (null until Calculate).
    public int? InstrumentCount { get; private set; }
    public int? NonComparableCount { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<InstrumentReading> Readings => _readings.AsReadOnly();

    public static InstrumentComparabilityStudy Configure(
        string studyRef, string analyte, string unit, string referenceInstrument, decimal allowableBiasPct)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("ICP-001", "An analyte is required.");
        }

        if (string.IsNullOrWhiteSpace(referenceInstrument))
        {
            throw new DomainException("ICP-002", "A reference instrument is required.");
        }

        if (allowableBiasPct is <= 0m or > 50m)
        {
            throw new DomainException("ICP-003", "The allowable bias must be a positive percentage (at most 50%).");
        }

        return new InstrumentComparabilityStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            ReferenceInstrument = referenceInstrument.Trim(),
            AllowableBiasPct = allowableBiasPct,
            State = InstrumentComparabilityState.DataEntry,
        };
    }

    public Guid AddReading(string instrument, string sampleId, decimal value)
    {
        RequireEditable();
        if (string.IsNullOrWhiteSpace(instrument) || string.IsNullOrWhiteSpace(sampleId))
        {
            throw new DomainException("ICP-004", "Both the instrument and the sample identifier are required.");
        }

        var reading = new InstrumentReading(instrument.Trim(), sampleId.Trim(), value);
        _readings.Add(reading);
        Invalidate();
        return reading.Id;
    }

    public void RemoveReading(Guid readingId)
    {
        RequireEditable();
        var reading = _readings.FirstOrDefault(r => r.Id == readingId)
            ?? throw new DomainException("ICP-404", "Reading not found.");
        _readings.Remove(reading);
        Invalidate();
    }

    public void Calculate()
    {
        RequireEditable();
        var reference = _readings
            .Where(r => string.Equals(r.Instrument, ReferenceInstrument, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(r => r.SampleId, r => r.Value, StringComparer.OrdinalIgnoreCase);

        if (reference.Count == 0)
        {
            throw new DomainException("ICP-010", "The reference instrument has no readings to compare against.");
        }

        var others = _readings
            .Where(r => !string.Equals(r.Instrument, ReferenceInstrument, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Instrument)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (others.Count == 0)
        {
            throw new DomainException("ICP-011", "At least one non-reference instrument is required.");
        }

        var results = Assess(reference);
        if (results.Any(r => r.PairedSamples == 0))
        {
            throw new DomainException("ICP-012", "Every instrument must share at least one sample with the reference.");
        }

        InstrumentCount = others.Count;
        NonComparableCount = results.Count(r => !r.Comparable);
        State = InstrumentComparabilityState.Calculated;
    }

    /// <summary>Per-instrument comparability table — derived from the readings, never persisted.</summary>
    public IReadOnlyList<InstrumentResult> Results()
    {
        if (State == InstrumentComparabilityState.DataEntry)
        {
            return [];
        }

        var reference = _readings
            .Where(r => string.Equals(r.Instrument, ReferenceInstrument, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(r => r.SampleId, r => r.Value, StringComparer.OrdinalIgnoreCase);
        return Assess(reference);
    }

    private IReadOnlyList<InstrumentResult> Assess(Dictionary<string, decimal> reference)
    {
        return _readings
            .Where(r => !string.Equals(r.Instrument, ReferenceInstrument, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.Instrument, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var biases = new List<double>();
                foreach (var reading in g)
                {
                    if (reference.TryGetValue(reading.SampleId, out var refValue) && refValue != 0m)
                    {
                        biases.Add((double)((reading.Value - refValue) / refValue) * 100.0);
                    }
                }

                var meanBias = biases.Count > 0 ? Math.Round((decimal)biases.Average(), 3) : 0m;
                return new InstrumentResult(
                    g.Key, biases.Count, meanBias,
                    biases.Count > 0 && Math.Abs(meanBias) <= AllowableBiasPct);
            })
            .ToList();
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        if (State != InstrumentComparabilityState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "ICP-013", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = InstrumentComparabilityState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new InstrumentComparabilitySignedOff(Id, StudyRef, Analyte, NonComparableCount!.Value, TenantId));
    }

    private void Invalidate()
    {
        InstrumentCount = NonComparableCount = null;
        if (State == InstrumentComparabilityState.Calculated)
        {
            State = InstrumentComparabilityState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == InstrumentComparabilityState.SignedOff)
        {
            throw new InvalidStateTransitionException("ICP-014", "A signed-off study is immutable.");
        }
    }
}

public sealed record InstrumentComparabilitySignedOff(
    Guid StudyId, string StudyRef, string Analyte, int NonComparableCount, Guid TenantId) : DomainEvent;
