using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.PatientSafety;

/// <summary>The patient-safety programme an event belongs to.</summary>
public enum SafetyEventType { Fall, PressureInjury }

/// <summary>Degree of harm reached, ordered least to most severe.</summary>
public enum HarmLevel { None, Minor, Moderate, Severe, Death }

/// <summary>Whether the injury was present on admission or acquired in hospital.</summary>
public enum InjuryOrigin { PresentOnAdmission, HospitalAcquired }

/// <summary>Pressure-injury staging (NPUAP/EPUAP).</summary>
public enum PressureInjuryStage { Stage1, Stage2, Stage3, Stage4, Unstageable, DeepTissueInjury }

/// <summary>Lifecycle of a safety event.</summary>
public enum SafetyEventStatus { Reported, Reviewed, Closed }

/// <summary>
/// A patient-safety event (HQMS M08): a fall or a pressure injury. Falls carry a harm level
/// and post-fall review; pressure injuries additionally carry a stage and a hospital-acquired
/// vs present-on-admission classification (the HAPI distinction that drives the reportable
/// rate). Reported → Reviewed → Closed; the event feeds the per-1,000-patient-day rate.
/// </summary>
public sealed class PatientSafetyEvent : AggregateRoot, ITenantScoped, IAllocatable
{
    private PatientSafetyEvent()
    {
        EventRef = null!;
        PatientRef = null!;
        Unit = null!;
        Description = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string EventRef { get; private set; }
    public SafetyEventType Type { get; private set; }
    public string PatientRef { get; private set; }
    public string Unit { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public HarmLevel HarmLevel { get; private set; }
    public InjuryOrigin Origin { get; private set; }
    public string Description { get; private set; }

    /// <summary>Pressure-injury stage — set only for pressure-injury events.</summary>
    public PressureInjuryStage? Stage { get; private set; }

    public SafetyEventStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? ReviewNotes { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public static PatientSafetyEvent ReportFall(
        string eventRef, string patientRef, string unit, DateTimeOffset occurredAtUtc,
        HarmLevel harm, string description, Guid? departmentId = null)
    {
        var e = Create(eventRef, SafetyEventType.Fall, patientRef, unit, occurredAtUtc, harm, description, departmentId);
        // A fall is by definition hospital-acquired; it has no pressure-injury stage.
        e.Origin = InjuryOrigin.HospitalAcquired;
        return e;
    }

    public static PatientSafetyEvent ReportPressureInjury(
        string eventRef, string patientRef, string unit, DateTimeOffset occurredAtUtc,
        HarmLevel harm, string description, PressureInjuryStage stage, InjuryOrigin origin,
        Guid? departmentId = null)
    {
        var e = Create(eventRef, SafetyEventType.PressureInjury, patientRef, unit, occurredAtUtc, harm, description, departmentId);
        e.Stage = stage;
        e.Origin = origin;
        return e;
    }

    private static PatientSafetyEvent Create(
        string eventRef, SafetyEventType type, string patientRef, string unit,
        DateTimeOffset occurredAtUtc, HarmLevel harm, string description, Guid? departmentId)
    {
        if (string.IsNullOrWhiteSpace(patientRef))
        {
            throw new DomainException("PSE-001", "A patient reference is required.");
        }

        if (occurredAtUtc == default)
        {
            throw new DomainException("PSE-002", "The time the event occurred is required.");
        }

        return new PatientSafetyEvent
        {
            EventRef = eventRef,
            Type = type,
            PatientRef = patientRef.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? "Unknown" : unit.Trim(),
            OccurredAtUtc = occurredAtUtc,
            HarmLevel = harm,
            Description = description?.Trim() ?? string.Empty,
            DepartmentId = departmentId,
            Status = SafetyEventStatus.Reported,
        };
    }

    /// <summary>Records the post-event review (Reported ⇒ Reviewed).</summary>
    public void RecordReview(Guid reviewerId, string notes, DateTimeOffset at)
    {
        if (Status != SafetyEventStatus.Reported)
        {
            throw new InvalidStateTransitionException("PSE-010", $"Cannot review an event in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainException("PSE-011", "Review notes are required.");
        }

        ReviewedBy = reviewerId;
        ReviewNotes = notes.Trim();
        ReviewedAtUtc = at;
        Status = SafetyEventStatus.Reviewed;
        Raise(new SafetyEventReviewed(Id, EventRef, Type.ToString()));
    }

    /// <summary>Closes the event after review (Reviewed ⇒ Closed).</summary>
    public void Close()
    {
        if (Status != SafetyEventStatus.Reviewed)
        {
            throw new InvalidStateTransitionException("PSE-012", "An event must be reviewed before it is closed.");
        }

        Status = SafetyEventStatus.Closed;
    }

    /// <summary>True for a hospital-acquired pressure injury (a HAPI — the reportable subset).</summary>
    public bool IsHospitalAcquiredPressureInjury =>
        Type == SafetyEventType.PressureInjury && Origin == InjuryOrigin.HospitalAcquired;
}

public sealed record SafetyEventReviewed(Guid SafetyEventId, string EventRef, string Type) : DomainEvent;
