using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Facility;

public enum MonitoringPointStatus { Active, Suspended, Retired }

/// <summary>
/// One recorded observation. InLimit is evaluated against the limits IN FORCE
/// at recording time and frozen — tightening limits later must not rewrite the
/// verdict history.
/// </summary>
public sealed class EnvironmentalReading : Entity
{
    internal EnvironmentalReading(
        decimal value, DateTimeOffset recordedAtUtc, Guid recordedById, bool inLimit, string? remark)
    {
        Value = value;
        RecordedAtUtc = recordedAtUtc;
        RecordedById = recordedById;
        InLimit = inLimit;
        Remark = remark;
    }

    private EnvironmentalReading() { }

    public decimal Value { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public Guid RecordedById { get; private set; }
    public bool InLimit { get; private set; }
    public string? Remark { get; private set; }
}

/// <summary>
/// Environmental/facility monitoring point (ISO 17025 §6.3 / ISO 15189 §6.3 /
/// GMP): a monitored parameter at a location (fridge temperature, room
/// humidity…) with acceptance limits. Readings are append-only children; a
/// reading outside the limits raises an excursion event that opens an NC —
/// results produced under bad conditions must be assessed. Limits can be
/// re-baselined (with the old values on the audit trail); readings never
/// change once recorded.
/// </summary>
public sealed class MonitoringPoint : AggregateRoot, ITenantScoped, IAllocatable
{
    private readonly List<EnvironmentalReading> _readings = [];

    private MonitoringPoint()
    {
        PointRef = null!;
        Name = null!;
        Parameter = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string PointRef { get; private set; }
    public string Name { get; private set; }
    public string? Location { get; private set; }
    /// <summary>The monitored quantity (LOV-managed), e.g. Temperature, Humidity, Pressure differential.</summary>
    public string Parameter { get; private set; }
    public string Unit { get; private set; }
    public decimal? LowLimit { get; private set; }
    public decimal? HighLimit { get; private set; }
    public MonitoringPointStatus Status { get; private set; }

    public IReadOnlyList<EnvironmentalReading> Readings => _readings.AsReadOnly();

    public static MonitoringPoint Register(
        string pointRef, string name, string? location, string parameter, string unit,
        decimal? lowLimit, decimal? highLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("ENV-001", "A monitoring point name is required.");
        }

        if (string.IsNullOrWhiteSpace(parameter) || string.IsNullOrWhiteSpace(unit))
        {
            throw new DomainException("ENV-002", "The monitored parameter and its unit are required.");
        }

        var point = new MonitoringPoint
        {
            PointRef = pointRef,
            Name = name.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Parameter = parameter.Trim(),
            Unit = unit.Trim(),
            Status = MonitoringPointStatus.Active,
        };
        point.SetLimits(lowLimit, highLimit);
        return point;
    }

    /// <summary>Re-baselines the acceptance limits; past readings keep the verdict they were recorded with.</summary>
    public void SetLimits(decimal? lowLimit, decimal? highLimit)
    {
        if (Status == MonitoringPointStatus.Retired)
        {
            throw new InvalidStateTransitionException("ENV-010", "A retired monitoring point cannot be re-baselined.");
        }

        if (lowLimit is null && highLimit is null)
        {
            throw new DomainException("ENV-003", "At least one acceptance limit (low or high) is required.");
        }

        if (lowLimit is not null && highLimit is not null && lowLimit >= highLimit)
        {
            throw new DomainException("ENV-004", "The low limit must fall below the high limit.");
        }

        LowLimit = lowLimit;
        HighLimit = highLimit;
    }

    /// <summary>
    /// Appends a reading; boundary values count as in-limit. An excursion
    /// raises the event that opens an NC via the improvement saga.
    /// </summary>
    public Guid RecordReading(decimal value, DateTimeOffset atUtc, Guid recordedById, string? remark)
    {
        if (Status != MonitoringPointStatus.Active)
        {
            throw new InvalidStateTransitionException("ENV-011", $"Readings can only be recorded on an active point (current: {Status}).");
        }

        var inLimit = (LowLimit is null || value >= LowLimit) && (HighLimit is null || value <= HighLimit);
        var reading = new EnvironmentalReading(
            value, atUtc, recordedById, inLimit,
            string.IsNullOrWhiteSpace(remark) ? null : remark.Trim());
        _readings.Add(reading);

        if (!inLimit)
        {
            Raise(new EnvironmentalExcursionDetected(
                Id, PointRef, Name, Parameter, Unit, value, LowLimit, HighLimit,
                reading.Id, recordedById, TenantId));
        }

        return reading.Id;
    }

    public void Suspend()
    {
        if (Status != MonitoringPointStatus.Active)
        {
            throw new InvalidStateTransitionException("ENV-012", $"Only an active point can be suspended (current: {Status}).");
        }

        Status = MonitoringPointStatus.Suspended;
    }

    public void Resume()
    {
        if (Status != MonitoringPointStatus.Suspended)
        {
            throw new InvalidStateTransitionException("ENV-013", $"Only a suspended point can be resumed (current: {Status}).");
        }

        Status = MonitoringPointStatus.Active;
    }

    public void Retire()
    {
        if (Status == MonitoringPointStatus.Retired)
        {
            throw new InvalidStateTransitionException("ENV-014", "The monitoring point is already retired.");
        }

        Status = MonitoringPointStatus.Retired;
    }
}

public sealed record EnvironmentalExcursionDetected(
    Guid PointId, string PointRef, string Name, string Parameter, string Unit,
    decimal Value, decimal? LowLimit, decimal? HighLimit,
    Guid ReadingId, Guid RecordedById, Guid TenantId) : DomainEvent;
