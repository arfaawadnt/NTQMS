using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Equipment;

public enum EquipmentStatus { NeedsCalibration, Active, OutOfService, Retired }

public sealed class CalibrationRecord : Entity
{
    internal CalibrationRecord(DateOnly performedAt, string provider, string result, Guid? certificateFileId)
    {
        PerformedAt = performedAt;
        Provider = provider;
        Result = result;
        CertificateFileId = certificateFileId;
    }

    private CalibrationRecord() { Provider = null!; Result = null!; }

    public DateOnly PerformedAt { get; private set; }
    public string Provider { get; private set; }
    public string Result { get; private set; }
    public Guid? CertificateFileId { get; private set; }
}

public sealed class MaintenanceRecord : Entity
{
    internal MaintenanceRecord(DateOnly performedAt, string workDescription)
    {
        PerformedAt = performedAt;
        WorkDescription = workDescription;
    }

    private MaintenanceRecord() { WorkDescription = null!; }

    public DateOnly PerformedAt { get; private set; }
    public string WorkDescription { get; private set; }
}

/// <summary>
/// Between-calibration confidence check (ISO 17025 §6.4.10): a zero/drift/
/// control check against a reference standard. A failed check questions every
/// result since the last good check, so it raises an event that opens an NC.
/// </summary>
public sealed class IntermediateCheck : Entity
{
    internal IntermediateCheck(
        DateOnly performedOn, Guid performedById, string checkType,
        bool passed, Guid? referenceStandardId, string? remarks)
    {
        PerformedOn = performedOn;
        PerformedById = performedById;
        CheckType = checkType;
        Passed = passed;
        ReferenceStandardId = referenceStandardId;
        Remarks = remarks;
    }

    private IntermediateCheck() { CheckType = null!; }

    public DateOnly PerformedOn { get; private set; }
    public Guid PerformedById { get; private set; }
    public string CheckType { get; private set; }
    public bool Passed { get; private set; }
    public Guid? ReferenceStandardId { get; private set; }
    public string? Remarks { get; private set; }
}

/// <summary>
/// Instrument/equipment with the canonical calibration state machine:
/// registered as NeedsCalibration (first calibration activates) → Active →
/// NeedsCalibration (due date reached) → OutOfService (grace exhausted) →
/// Active (calibration logged) → Retired. The scheduled sweep PROPOSES the
/// due/lockout transitions; this aggregate decides — a job can never bypass
/// the guards (FR-GOV-01 / FR-EQUIP-LOCK).
/// </summary>
public sealed class EquipmentItem : AggregateRoot, ITenantScoped, IAllocatable
{
    private readonly List<CalibrationRecord> _calibrations = [];
    private readonly List<MaintenanceRecord> _maintenance = [];
    private readonly List<IntermediateCheck> _intermediateChecks = [];

    private EquipmentItem()
    {
        Code = null!;
        Name = null!;
        SerialNumber = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string SerialNumber { get; private set; }
    public string? Location { get; private set; }
    public EquipmentStatus Status { get; private set; }
    public int CalibrationIntervalDays { get; private set; }
    public int GracePeriodDays { get; private set; }
    public DateOnly? LastCalibrationAt { get; private set; }
    public DateOnly? NextCalibrationDue { get; private set; }

    public IReadOnlyList<CalibrationRecord> Calibrations => _calibrations.AsReadOnly();
    public IReadOnlyList<MaintenanceRecord> Maintenance => _maintenance.AsReadOnly();
    public IReadOnlyList<IntermediateCheck> IntermediateChecks => _intermediateChecks.AsReadOnly();

    public static EquipmentItem Register(
        string code, string name, string serialNumber, string? location,
        int calibrationIntervalDays, int gracePeriodDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("EQP-001", "Equipment name is required.");
        }

        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new DomainException("EQP-002", "Serial number is required.");
        }

        if (calibrationIntervalDays < 1 || gracePeriodDays < 0)
        {
            throw new DomainException("EQP-003", "Calibration interval must be positive; grace period non-negative.");
        }

        return new EquipmentItem
        {
            Code = code,
            Name = name.Trim(),
            SerialNumber = serialNumber.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Status = EquipmentStatus.NeedsCalibration, // First calibration activates.
            CalibrationIntervalDays = calibrationIntervalDays,
            GracePeriodDays = gracePeriodDays,
        };
    }

    public void LogCalibration(DateOnly performedAt, string provider, string result, Guid? certificateFileId)
    {
        if (Status == EquipmentStatus.Retired)
        {
            throw new InvalidStateTransitionException("EQP-010", "Retired equipment cannot be calibrated.");
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new DomainException("EQP-011", "A calibration result is required.");
        }

        _calibrations.Add(new CalibrationRecord(
            performedAt, provider?.Trim() ?? string.Empty, result.Trim(), certificateFileId));

        var wasOutOfUse = Status != EquipmentStatus.Active;
        LastCalibrationAt = performedAt;
        NextCalibrationDue = performedAt.AddDays(CalibrationIntervalDays);
        Status = EquipmentStatus.Active;

        if (wasOutOfUse)
        {
            Raise(new EquipmentReturnedToService(Id, Code, TenantId));
        }
    }

    /// <summary>Sweep-proposed: Active + due date reached → NeedsCalibration.</summary>
    public void MarkCalibrationDue(DateOnly asOf)
    {
        if (Status != EquipmentStatus.Active || NextCalibrationDue is null || NextCalibrationDue > asOf)
        {
            return; // Proposal declined — not actually due.
        }

        Status = EquipmentStatus.NeedsCalibration;
        Raise(new CalibrationDue(Id, Code, Name, NextCalibrationDue.Value, TenantId));
    }

    /// <summary>Sweep-proposed: NeedsCalibration + grace exhausted → OutOfService (auto-lockout).</summary>
    public void LockOutIfGraceExhausted(DateOnly asOf)
    {
        if (Status != EquipmentStatus.NeedsCalibration || NextCalibrationDue is null)
        {
            return;
        }

        if (NextCalibrationDue.Value.AddDays(GracePeriodDays) >= asOf)
        {
            return; // Still within grace.
        }

        Status = EquipmentStatus.OutOfService;
        Raise(new EquipmentLockedOut(Id, Code, Name, TenantId));
    }

    public void LogMaintenance(DateOnly performedAt, string workDescription)
    {
        if (Status == EquipmentStatus.Retired)
        {
            throw new InvalidStateTransitionException("EQP-012", "Retired equipment cannot receive maintenance.");
        }

        if (string.IsNullOrWhiteSpace(workDescription))
        {
            throw new DomainException("EQP-013", "Maintenance description is required.");
        }

        _maintenance.Add(new MaintenanceRecord(performedAt, workDescription.Trim()));
    }

    /// <summary>
    /// Records a between-calibration confidence check (§6.4.10). A failure
    /// raises IntermediateCheckFailed so the improvement context opens an NC —
    /// results since the last good check may be affected.
    /// </summary>
    public Guid RecordIntermediateCheck(
        DateOnly performedOn, Guid performedById, string checkType,
        bool passed, Guid? referenceStandardId, string? remarks)
    {
        if (Status == EquipmentStatus.Retired)
        {
            throw new InvalidStateTransitionException("EQP-020", "Retired equipment cannot receive intermediate checks.");
        }

        if (string.IsNullOrWhiteSpace(checkType))
        {
            throw new DomainException("EQP-021", "A check type is required.");
        }

        var check = new IntermediateCheck(
            performedOn, performedById, checkType.Trim(), passed, referenceStandardId,
            string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim());
        _intermediateChecks.Add(check);

        if (!passed)
        {
            Raise(new IntermediateCheckFailed(
                Id, Code, Name, check.Id, check.CheckType, performedOn, performedById, TenantId));
        }

        return check.Id;
    }

    public void Retire()
    {
        if (Status == EquipmentStatus.Retired)
        {
            throw new InvalidStateTransitionException("EQP-014", "Equipment is already retired.");
        }

        Status = EquipmentStatus.Retired;
        Raise(new EquipmentRetired(Id, Code, TenantId));
    }
}

public sealed record CalibrationDue(
    Guid EquipmentId, string Code, string Name, DateOnly DueDate, Guid TenantId) : DomainEvent;

public sealed record EquipmentLockedOut(Guid EquipmentId, string Code, string Name, Guid TenantId) : DomainEvent;

public sealed record EquipmentReturnedToService(Guid EquipmentId, string Code, Guid TenantId) : DomainEvent;

public sealed record EquipmentRetired(Guid EquipmentId, string Code, Guid TenantId) : DomainEvent;

public sealed record IntermediateCheckFailed(
    Guid EquipmentId, string Code, string Name, Guid CheckId, string CheckType,
    DateOnly PerformedOn, Guid PerformedById, Guid TenantId) : DomainEvent;
