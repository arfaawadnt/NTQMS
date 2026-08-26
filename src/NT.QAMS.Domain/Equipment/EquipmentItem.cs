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
    internal MaintenanceRecord(DateOnly performedAt, string workDescription, Guid? certificateFileId)
    {
        PerformedAt = performedAt;
        WorkDescription = workDescription;
        CertificateFileId = certificateFileId;
    }

    private MaintenanceRecord() { WorkDescription = null!; }

    public DateOnly PerformedAt { get; private set; }
    public string WorkDescription { get; private set; }

    /// <summary>Optional service/maintenance certificate, mirroring the calibration record.</summary>
    public Guid? CertificateFileId { get; private set; }
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

/// <summary>Why a device was out of use — distinguishes unplanned breakdown from planned service.</summary>
public enum DowntimeCategory { Breakdown, AwaitingParts, ScheduledMaintenance, Other }

/// <summary>
/// A period a device was unavailable (HQMS M14). Open while the device is down; closed when it
/// returns to use. Downtime hours across a window drive the availability figure.
/// </summary>
public sealed class DowntimeEvent : Entity
{
    internal DowntimeEvent(DateTimeOffset startedAtUtc, DowntimeCategory category, string reason)
    {
        StartedAtUtc = startedAtUtc;
        Category = category;
        Reason = reason;
    }

    private DowntimeEvent() { Reason = null!; }

    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }
    public DowntimeCategory Category { get; private set; }
    public string Reason { get; private set; }

    public bool IsOpen => EndedAtUtc is null;

    /// <summary>Downtime hours accrued to <paramref name="asOf"/> (or to the end, if closed).</summary>
    public double DurationHours(DateTimeOffset asOf)
    {
        var end = EndedAtUtc ?? asOf;
        return end <= StartedAtUtc ? 0 : (end - StartedAtUtc).TotalHours;
    }

    internal void End(DateTimeOffset endedAtUtc) => EndedAtUtc = endedAtUtc;
}

/// <summary>The kind of manufacturer/regulator safety communication logged against a device.</summary>
public enum SafetyNoticeType { Recall, FieldSafetyNotice, HazardAlert }

/// <summary>Severity of a safety notice.</summary>
public enum SafetyNoticeSeverity { Low, Medium, High }

/// <summary>Lifecycle of a safety notice: open on receipt, actioned, then closed.</summary>
public enum SafetyNoticeStatus { Open, Actioned, Closed }

/// <summary>
/// A recall / field safety notice / hazard alert affecting a device (HQMS M14): logged on receipt
/// with a required-action deadline, actioned with a note, then closed. Open notices past their
/// deadline are the safety backlog on the recall register.
/// </summary>
public sealed class SafetyNotice : Entity
{
    internal SafetyNotice(
        SafetyNoticeType type, string reference, string issuer, SafetyNoticeSeverity severity,
        DateOnly receivedOn, DateOnly? requiredActionBy)
    {
        Type = type;
        Reference = reference;
        Issuer = issuer;
        Severity = severity;
        ReceivedOn = receivedOn;
        RequiredActionBy = requiredActionBy;
        Status = SafetyNoticeStatus.Open;
    }

    private SafetyNotice() { Reference = null!; Issuer = null!; }

    public SafetyNoticeType Type { get; private set; }
    public string Reference { get; private set; }
    public string Issuer { get; private set; }
    public SafetyNoticeSeverity Severity { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public DateOnly? RequiredActionBy { get; private set; }
    public SafetyNoticeStatus Status { get; private set; }
    public string? ActionNote { get; private set; }
    public DateOnly? ActionedOn { get; private set; }

    public bool IsOverdue(DateOnly asOf) => Status == SafetyNoticeStatus.Open && RequiredActionBy is { } by && asOf > by;

    internal void Action(string note, DateOnly on) { Status = SafetyNoticeStatus.Actioned; ActionNote = note; ActionedOn = on; }
    internal void Close() => Status = SafetyNoticeStatus.Closed;
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
    private readonly List<DowntimeEvent> _downtime = [];
    private readonly List<SafetyNotice> _safetyNotices = [];

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
    public IReadOnlyList<DowntimeEvent> Downtime => _downtime.AsReadOnly();
    public IReadOnlyList<SafetyNotice> SafetyNotices => _safetyNotices.AsReadOnly();

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

    public void LogMaintenance(DateOnly performedAt, string workDescription, Guid? certificateFileId = null)
    {
        if (Status == EquipmentStatus.Retired)
        {
            throw new InvalidStateTransitionException("EQP-012", "Retired equipment cannot receive maintenance.");
        }

        if (string.IsNullOrWhiteSpace(workDescription))
        {
            throw new DomainException("EQP-013", "Maintenance description is required.");
        }

        _maintenance.Add(new MaintenanceRecord(performedAt, workDescription.Trim(), certificateFileId));
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

    // ── Downtime & availability (HQMS M14) ──────────────────────────────────────

    /// <summary>Opens a downtime period for the device. Only one may be open at a time.</summary>
    public Guid StartDowntime(DateTimeOffset startedAtUtc, DowntimeCategory category, string reason)
    {
        if (Status == EquipmentStatus.Retired)
        {
            throw new InvalidStateTransitionException("EQP-030", "Retired equipment cannot accrue downtime.");
        }

        if (_downtime.Any(d => d.IsOpen))
        {
            throw new DomainException("EQP-031", "An open downtime period already exists; end it before starting another.");
        }

        var evt = new DowntimeEvent(startedAtUtc, category, string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason.Trim());
        _downtime.Add(evt);
        return evt.Id;
    }

    /// <summary>Closes the open downtime period.</summary>
    public void EndDowntime(Guid downtimeId, DateTimeOffset endedAtUtc)
    {
        var evt = _downtime.FirstOrDefault(d => d.Id == downtimeId)
            ?? throw new DomainException("EQP-032", "Downtime event not found.");
        if (!evt.IsOpen)
        {
            throw new InvalidStateTransitionException("EQP-033", "The downtime period is already ended.");
        }

        if (endedAtUtc < evt.StartedAtUtc)
        {
            throw new DomainException("EQP-034", "Downtime cannot end before it started.");
        }

        evt.End(endedAtUtc);
    }

    /// <summary>
    /// Availability over a window [from, asOf]: uptime ÷ window, where downtime is each event's
    /// hours clipped to the window. Returns a fraction 0–1.
    /// </summary>
    public double Availability(DateTimeOffset from, DateTimeOffset asOf)
    {
        var windowHours = (asOf - from).TotalHours;
        if (windowHours <= 0)
        {
            return 1;
        }

        var down = _downtime.Sum(d =>
        {
            var start = d.StartedAtUtc > from ? d.StartedAtUtc : from;
            var end = (d.EndedAtUtc ?? asOf) < asOf ? (d.EndedAtUtc ?? asOf) : asOf;
            return end <= start ? 0 : (end - start).TotalHours;
        });

        var availability = 1 - (down / windowHours);
        return availability < 0 ? 0 : availability;
    }

    // ── Recalls & field safety notices (HQMS M14) ───────────────────────────────

    public Guid LogSafetyNotice(
        SafetyNoticeType type, string reference, string issuer, SafetyNoticeSeverity severity,
        DateOnly receivedOn, DateOnly? requiredActionBy)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new DomainException("EQP-040", "A notice reference is required.");
        }

        var notice = new SafetyNotice(
            type, reference.Trim(), string.IsNullOrWhiteSpace(issuer) ? "Unknown" : issuer.Trim(),
            severity, receivedOn, requiredActionBy);
        _safetyNotices.Add(notice);
        return notice.Id;
    }

    public void ActionSafetyNotice(Guid noticeId, string note, DateOnly on)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException("EQP-041", "An action note is required.");
        }

        var notice = LoadOpenNotice(noticeId);
        notice.Action(note.Trim(), on);
    }

    public void CloseSafetyNotice(Guid noticeId)
    {
        var notice = _safetyNotices.FirstOrDefault(n => n.Id == noticeId)
            ?? throw new DomainException("EQP-042", "Safety notice not found.");
        if (notice.Status != SafetyNoticeStatus.Actioned)
        {
            throw new InvalidStateTransitionException("EQP-043", "A safety notice must be actioned before it is closed.");
        }

        notice.Close();
    }

    private SafetyNotice LoadOpenNotice(Guid noticeId)
    {
        var notice = _safetyNotices.FirstOrDefault(n => n.Id == noticeId)
            ?? throw new DomainException("EQP-042", "Safety notice not found.");
        if (notice.Status != SafetyNoticeStatus.Open)
        {
            throw new InvalidStateTransitionException("EQP-044", $"A safety notice in state {notice.Status} cannot be actioned.");
        }

        return notice;
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
