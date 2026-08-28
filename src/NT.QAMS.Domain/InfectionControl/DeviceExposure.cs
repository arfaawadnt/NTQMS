using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.InfectionControl;

/// <summary>An invasive device whose in-place time is surveilled for device-associated infection.</summary>
public enum DeviceType { CentralLine, UrinaryCatheter, Ventilator }

/// <summary>Whether the device is currently in place or has been removed.</summary>
public enum DeviceStatus { InPlace, Removed }

/// <summary>
/// A device-exposure line (HQMS M09): one invasive device in one patient, from insertion to
/// removal. The device-days accrued across all exposures of a type are the denominator for the
/// device-associated infection rate (CLABSI per 1,000 central-line-days, etc.) and, against the
/// M24 patient-days, the device-utilisation ratio. Only what the rate maths needs is stored —
/// a pseudonymised patient reference and the unit — never the clinical record (ADR-4).
/// </summary>
public sealed class DeviceExposure : AggregateRoot, ITenantScoped
{
    private DeviceExposure()
    {
        PatientRef = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; private set; }

    /// <summary>Pseudonymised patient identifier (never the clinical MRN in clear).</summary>
    public string PatientRef { get; private set; }

    public string Unit { get; private set; }
    public DeviceType DeviceType { get; private set; }
    public DateTimeOffset InsertedAtUtc { get; private set; }
    public DateTimeOffset? RemovedAtUtc { get; private set; }
    public DeviceStatus Status { get; private set; }

    public static DeviceExposure Record(
        string patientRef, string unit, DeviceType deviceType, DateTimeOffset insertedAtUtc, Guid? departmentId = null)
    {
        if (string.IsNullOrWhiteSpace(patientRef))
        {
            throw new DomainException("DEV-001", "A patient reference is required.");
        }

        if (insertedAtUtc == default)
        {
            throw new DomainException("DEV-002", "The device insertion time is required.");
        }

        return new DeviceExposure
        {
            PatientRef = patientRef.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? "Unknown" : unit.Trim(),
            DeviceType = deviceType,
            InsertedAtUtc = insertedAtUtc,
            DepartmentId = departmentId,
            Status = DeviceStatus.InPlace,
        };
    }

    /// <summary>Records device removal (idempotent: a repeated removal keeps the first time).</summary>
    public void Remove(DateTimeOffset at)
    {
        if (Status == DeviceStatus.Removed)
        {
            return; // Idempotent — a duplicate removal event is a no-op.
        }

        if (at < InsertedAtUtc)
        {
            throw new DomainException("DEV-010", "Device removal cannot precede insertion.");
        }

        RemovedAtUtc = at;
        Status = DeviceStatus.Removed;
    }

    /// <summary>
    /// Device-days accrued up to <paramref name="asOf"/> (or removal, if earlier). A device in
    /// place on the day of insertion counts as at least one device-day, mirroring patient-days.
    /// </summary>
    public int DeviceDays(DateTimeOffset asOf) =>
        WindowedDays.Clipped(InsertedAtUtc, RemovedAtUtc, InsertedAtUtc, asOf);

    /// <summary>
    /// Device-days this exposure contributes to the window [<paramref name="from"/>,
    /// <paramref name="asOf"/>] — the canonical rate denominator every module shares.
    /// </summary>
    public int DeviceDaysInWindow(DateTimeOffset from, DateTimeOffset asOf) =>
        WindowedDays.Clipped(InsertedAtUtc, RemovedAtUtc, from, asOf);
}
