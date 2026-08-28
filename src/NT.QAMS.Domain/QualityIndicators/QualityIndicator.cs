using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.QualityIndicators;

/// <summary>How often an indicator is collected.</summary>
public enum IndicatorFrequency { Weekly, Monthly, Quarterly, Annually }

/// <summary>
/// Whether a higher measured value is good or bad — decides which side of the
/// thresholds counts as a breach. A hand-hygiene rate is higher-is-better; a
/// fall rate is lower-is-better.
/// </summary>
public enum IndicatorDirection { HigherIsBetter, LowerIsBetter }

/// <summary>Lifecycle of an indicator definition.</summary>
public enum IndicatorStatus { Active, Retired }

/// <summary>Where a single period's value sits against the governed thresholds.</summary>
public enum MeasurementStatus { InTarget, Warning, Breached }

/// <summary>
/// One period's governed measurement. The value is computed from the numerator and
/// denominator at entry and never recomputed, so the number quoted to the board and
/// the number quoted to a surveyor are the same number even if the definition later
/// changes. The status is the verdict against the thresholds in force at entry.
/// </summary>
public sealed class IndicatorMeasurement : Entity
{
    internal IndicatorMeasurement(
        DateOnly period, decimal numerator, decimal denominator, decimal value,
        MeasurementStatus status, Guid enteredBy, DateTimeOffset recordedAtUtc, string? note)
    {
        Period = period;
        Numerator = numerator;
        Denominator = denominator;
        Value = value;
        Status = status;
        EnteredBy = enteredBy;
        RecordedAtUtc = recordedAtUtc;
        Note = note;
    }

    private IndicatorMeasurement() { }

    /// <summary>First day of the period the value covers (month/quarter/etc.).</summary>
    public DateOnly Period { get; private set; }
    public decimal Numerator { get; private set; }
    public decimal Denominator { get; private set; }

    /// <summary>The computed rate (numerator ÷ denominator × the indicator's rate factor).</summary>
    public decimal Value { get; private set; }
    public MeasurementStatus Status { get; private set; }
    public Guid EnteredBy { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public string? Note { get; private set; }
}

/// <summary>
/// A governed quality indicator (HQMS M06). Holds the formal data dictionary — the
/// definitions that make a number defensible — its target and action thresholds, and
/// the period measurements collected against it. Breaching the action threshold raises
/// a domain event so the measurement-to-action loop can close (analysis task, then CAPA
/// when sustained). Retired indicators keep their history but accept no new measurements.
/// </summary>
public sealed class QualityIndicator : AggregateRoot, ITenantScoped
{
    private readonly List<IndicatorMeasurement> _measurements = [];

    private QualityIndicator()
    {
        IndicatorRef = null!;
        Code = null!;
        Name = null!;
        Numerator = null!;
        Denominator = null!;
        Unit = null!;
    }

    /// <inheritdoc />
    public Guid TenantId { get; set; }

    /// <summary>Human-readable reference, e.g. <c>IND-2026-0001</c>.</summary>
    public string IndicatorRef { get; private set; }

    /// <summary>Short stable code the hospital uses for the indicator (e.g. <c>IPSG-1</c>).</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    // ── Data dictionary ──────────────────────────────────────────────────────
    public string Numerator { get; private set; }
    public string Denominator { get; private set; }
    public string? Inclusions { get; private set; }
    public string? Exclusions { get; private set; }
    public string? DataSource { get; private set; }
    public IndicatorFrequency Frequency { get; private set; }

    /// <summary>Unit label for display (e.g. "%", "per 1,000 patient-days").</summary>
    public string Unit { get; private set; }

    /// <summary>Multiplier applied to numerator ÷ denominator (100 for a percentage, 1000 for a rate per 1,000).</summary>
    public decimal RateFactor { get; private set; }

    public IndicatorDirection Direction { get; private set; }
    public decimal? Target { get; private set; }
    public decimal? WarningThreshold { get; private set; }
    public decimal? ActionThreshold { get; private set; }
    public IndicatorStatus Status { get; private set; }

    public IReadOnlyList<IndicatorMeasurement> Measurements => _measurements.AsReadOnly();

    public static QualityIndicator Define(
        string indicatorRef, string code, string name, string? description,
        string numerator, string denominator, string unit, decimal rateFactor,
        IndicatorFrequency frequency, IndicatorDirection direction,
        string? inclusions = null, string? exclusions = null, string? dataSource = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("IND-001", "An indicator code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("IND-002", "An indicator name is required.");
        }

        if (string.IsNullOrWhiteSpace(numerator) || string.IsNullOrWhiteSpace(denominator))
        {
            throw new DomainException("IND-003", "Numerator and denominator definitions are required.");
        }

        if (rateFactor <= 0m)
        {
            throw new DomainException("IND-004", "The rate factor must be positive.");
        }

        return new QualityIndicator
        {
            IndicatorRef = indicatorRef,
            Code = code.Trim(),
            Name = name.Trim(),
            Description = Clean(description),
            Numerator = numerator.Trim(),
            Denominator = denominator.Trim(),
            Unit = unit.Trim(),
            RateFactor = rateFactor,
            Frequency = frequency,
            Direction = direction,
            Inclusions = Clean(inclusions),
            Exclusions = Clean(exclusions),
            DataSource = Clean(dataSource),
            Status = IndicatorStatus.Active,
        };
    }

    /// <summary>Amends the data-dictionary text. Does not recompute existing measurements.</summary>
    public void UpdateDefinition(
        string name, string? description, string numerator, string denominator, string unit,
        decimal rateFactor, IndicatorFrequency frequency, IndicatorDirection direction,
        string? inclusions, string? exclusions, string? dataSource)
    {
        EnsureActive("IND-010", "amend");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("IND-002", "An indicator name is required.");
        }

        if (string.IsNullOrWhiteSpace(numerator) || string.IsNullOrWhiteSpace(denominator))
        {
            throw new DomainException("IND-003", "Numerator and denominator definitions are required.");
        }

        if (rateFactor <= 0m)
        {
            throw new DomainException("IND-004", "The rate factor must be positive.");
        }

        Name = name.Trim();
        Description = Clean(description);
        Numerator = numerator.Trim();
        Denominator = denominator.Trim();
        Unit = unit.Trim();
        RateFactor = rateFactor;
        Frequency = frequency;
        Direction = direction;
        Inclusions = Clean(inclusions);
        Exclusions = Clean(exclusions);
        DataSource = Clean(dataSource);
    }

    /// <summary>
    /// Sets the target and the warning/action thresholds. Consistency is enforced per
    /// direction: for a higher-is-better indicator the action floor must sit at or below
    /// the warning level (both are "too low" limits); for lower-is-better it is reversed.
    /// </summary>
    public void SetTargets(decimal? target, decimal? warningThreshold, decimal? actionThreshold)
    {
        EnsureActive("IND-011", "set targets on");

        if (warningThreshold is { } w && actionThreshold is { } a)
        {
            var consistent = Direction == IndicatorDirection.HigherIsBetter ? a <= w : a >= w;
            if (!consistent)
            {
                throw new DomainException(
                    "IND-012",
                    "The action threshold must be beyond the warning threshold in the worsening direction.");
            }
        }

        Target = target;
        WarningThreshold = warningThreshold;
        ActionThreshold = actionThreshold;
    }

    /// <summary>
    /// Records a period's value from its numerator and denominator, computes the rate,
    /// grades it against the thresholds, and — on an action breach — raises the event
    /// that opens the analysis task. One measurement per period, where the period is
    /// normalized to the frequency's canonical start day (M-17): raw-date equality let
    /// one month carry two governed numbers, two SPC points and two breach tasks.
    /// </summary>
    public Guid RecordMeasurement(
        DateOnly period, decimal numerator, decimal denominator, Guid enteredBy,
        DateTimeOffset recordedAtUtc, string? note = null)
    {
        EnsureActive("IND-013", "record a measurement on");

        if (denominator <= 0m)
        {
            throw new DomainException("IND-014", "The denominator must be greater than zero.");
        }

        if (numerator < 0m)
        {
            throw new DomainException("IND-015", "The numerator cannot be negative.");
        }

        period = NormalizePeriod(period);
        if (_measurements.Any(m => m.Period == period))
        {
            throw new DomainException("IND-016", $"A measurement for {period:yyyy-MM-dd} already exists.");
        }

        var value = decimal.Round(numerator / denominator * RateFactor, 4);
        var status = Grade(value);

        var measurement = new IndicatorMeasurement(
            period, numerator, denominator, value, status, enteredBy, recordedAtUtc, Clean(note));
        _measurements.Add(measurement);

        Raise(new IndicatorMeasured(Id, IndicatorRef, Code, period, value, status));
        if (status == MeasurementStatus.Breached)
        {
            Raise(new IndicatorBreached(Id, IndicatorRef, Code, period, value, ActionThreshold ?? value));
        }

        return measurement.Id;
    }

    /// <summary>
    /// The canonical first day of the period containing <paramref name="date"/> for this
    /// indicator's frequency — Monday for Weekly, the 1st for Monthly, the quarter's first
    /// day for Quarterly, 1 January for Annually.
    /// </summary>
    public DateOnly NormalizePeriod(DateOnly date) => Frequency switch
    {
        IndicatorFrequency.Weekly => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        IndicatorFrequency.Monthly => new DateOnly(date.Year, date.Month, 1),
        IndicatorFrequency.Quarterly => new DateOnly(date.Year, (((date.Month - 1) / 3) * 3) + 1, 1),
        IndicatorFrequency.Annually => new DateOnly(date.Year, 1, 1),
        _ => date,
    };

    /// <summary>Retires the indicator; history is kept but no new measurements are accepted.</summary>
    public void Retire()
    {
        EnsureActive("IND-017", "retire");
        Status = IndicatorStatus.Retired;
    }

    /// <summary>Grades a value against the thresholds honouring the indicator's direction.</summary>
    private MeasurementStatus Grade(decimal value)
    {
        if (Direction == IndicatorDirection.HigherIsBetter)
        {
            if (ActionThreshold is { } a && value <= a) { return MeasurementStatus.Breached; }
            if (WarningThreshold is { } w && value <= w) { return MeasurementStatus.Warning; }
        }
        else
        {
            if (ActionThreshold is { } a && value >= a) { return MeasurementStatus.Breached; }
            if (WarningThreshold is { } w && value >= w) { return MeasurementStatus.Warning; }
        }

        return MeasurementStatus.InTarget;
    }

    private void EnsureActive(string code, string action)
    {
        if (Status != IndicatorStatus.Active)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} a retired indicator.");
        }
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed record IndicatorMeasured(
    Guid IndicatorId, string IndicatorRef, string Code, DateOnly Period, decimal Value, MeasurementStatus Status) : DomainEvent;

public sealed record IndicatorBreached(
    Guid IndicatorId, string IndicatorRef, string Code, DateOnly Period, decimal Value, decimal ActionThreshold) : DomainEvent;
