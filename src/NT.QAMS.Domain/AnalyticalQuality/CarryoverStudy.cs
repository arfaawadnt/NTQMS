using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum CarryoverState { DataEntry, Calculated, SignedOff }

public enum CarryoverSampleKind { High, Low }

/// <summary>One reading in the high→low carryover sequence, ordered by Sequence.</summary>
public sealed class CarryoverReading : Entity
{
    internal CarryoverReading(CarryoverSampleKind kind, int sequence, decimal value)
    {
        Kind = kind;
        Sequence = sequence;
        Value = value;
    }

    private CarryoverReading() { }

    public CarryoverSampleKind Kind { get; private set; }
    /// <summary>Position of the reading within its group (low readings are evaluated in order).</summary>
    public int Sequence { get; private set; }
    public decimal Value { get; private set; }
}

/// <summary>
/// Sample-carryover study (CLSI EP10-style): a high-concentration sample is run,
/// then low-concentration samples in sequence. Carryover is the low reading
/// immediately after the high, relative to the low steady state:
/// carryover% = (firstLow − steadyLow) / (meanHigh − steadyLow) × 100, where
/// steadyLow is the mean of the later low readings. It passes when the absolute
/// carryover is within the allowable limit. Readings are immutable; the result
/// is derivable-only; sign-off freezes the study.
/// </summary>
public sealed class CarryoverStudy : AggregateRoot, ITenantScoped
{
    public const int MinimumHigh = 1;
    public const int MinimumLow = 3;

    private readonly List<CarryoverReading> _readings = [];

    private CarryoverStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    /// <summary>Maximum acceptable carryover, as a percentage.</summary>
    public decimal AllowableCarryoverPct { get; private set; }
    public CarryoverState State { get; private set; }

    // Derived (null until Calculate).
    public decimal? MeanHigh { get; private set; }
    public decimal? FirstLow { get; private set; }
    public decimal? SteadyLow { get; private set; }
    public decimal? CarryoverPct { get; private set; }
    public bool? Passes { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<CarryoverReading> Readings => _readings.AsReadOnly();

    public static CarryoverStudy Configure(
        string studyRef, string analyte, string unit, decimal allowableCarryoverPct)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("CAR-001", "An analyte is required.");
        }

        if (allowableCarryoverPct is <= 0m or > 50m)
        {
            throw new DomainException("CAR-002", "The allowable carryover must be a positive percentage (at most 50%).");
        }

        return new CarryoverStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            AllowableCarryoverPct = allowableCarryoverPct,
            State = CarryoverState.DataEntry,
        };
    }

    public Guid AddReading(CarryoverSampleKind kind, int sequence, decimal value)
    {
        RequireEditable();
        var reading = new CarryoverReading(kind, sequence, value);
        _readings.Add(reading);
        Invalidate();
        return reading.Id;
    }

    public void RemoveReading(Guid readingId)
    {
        RequireEditable();
        var reading = _readings.FirstOrDefault(r => r.Id == readingId)
            ?? throw new DomainException("CAR-404", "Reading not found.");
        _readings.Remove(reading);
        Invalidate();
    }

    public void Calculate()
    {
        RequireEditable();
        var highs = _readings.Where(r => r.Kind == CarryoverSampleKind.High).ToList();
        var lows = _readings.Where(r => r.Kind == CarryoverSampleKind.Low).OrderBy(r => r.Sequence).ToList();

        if (highs.Count < MinimumHigh)
        {
            throw new DomainException("CAR-010", "At least one high-concentration reading is required.");
        }

        if (lows.Count < MinimumLow)
        {
            throw new DomainException("CAR-011", $"At least {MinimumLow} low readings are required (first low + steady state).");
        }

        var meanHigh = highs.Average(r => r.Value);
        var firstLow = lows[0].Value;
        var steadyLow = lows.Skip(1).Average(r => r.Value);

        if (meanHigh == steadyLow)
        {
            throw new DomainException("CAR-012", "The high and low steady-state means are equal — carryover cannot be computed.");
        }

        var carryover = (firstLow - steadyLow) / (meanHigh - steadyLow) * 100m;
        MeanHigh = Math.Round(meanHigh, 4);
        FirstLow = firstLow;
        SteadyLow = Math.Round(steadyLow, 4);
        CarryoverPct = Math.Round(carryover, 4);
        Passes = Math.Abs(CarryoverPct.Value) <= AllowableCarryoverPct;
        State = CarryoverState.Calculated;
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001");
        if (State != CarryoverState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "CAR-013", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = CarryoverState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new CarryoverStudySignedOff(Id, StudyRef, Analyte, Passes!.Value, TenantId));
    }

    private void Invalidate()
    {
        MeanHigh = FirstLow = SteadyLow = CarryoverPct = null;
        Passes = null;
        if (State == CarryoverState.Calculated)
        {
            State = CarryoverState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == CarryoverState.SignedOff)
        {
            throw new InvalidStateTransitionException("CAR-014", "A signed-off study is immutable.");
        }
    }
}

public sealed record CarryoverStudySignedOff(
    Guid StudyId, string StudyRef, string Analyte, bool Passes, Guid TenantId) : DomainEvent;
