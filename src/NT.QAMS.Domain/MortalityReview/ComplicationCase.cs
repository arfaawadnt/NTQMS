using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.MortalityReview;

/// <summary>The kind of complication captured in the morbidity register.</summary>
public enum ComplicationType { ReturnToTheatre, UnplannedIcuAdmission, UnplannedReadmission, HospitalAcquiredCondition, Other }

/// <summary>Severity of the complication.</summary>
public enum ComplicationSeverity { Minor, Moderate, Severe, LifeThreatening }

/// <summary>Lifecycle of a complication case.</summary>
public enum ComplicationStatus { Reported, Reviewed, Closed, Rejected }

/// <summary>
/// A complication / morbidity case (HQMS M10): the morbidity register captures events such as an
/// unplanned return to theatre, unplanned ICU admission or readmission. Reported, peer-reviewed
/// (with a preventability judgement), then closed.
/// </summary>
public sealed class ComplicationCase : AggregateRoot, ITenantScoped
{
    private ComplicationCase()
    {
        CaseRef = null!;
        PatientRef = null!;
        Unit = null!;
        Description = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; private set; }
    public string CaseRef { get; private set; }
    public string PatientRef { get; private set; }
    public string Unit { get; private set; }
    public ComplicationType Type { get; private set; }
    public ComplicationSeverity Severity { get; private set; }
    public DateTimeOffset OccurredDateUtc { get; private set; }
    public string Description { get; private set; }

    public ComplicationStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? ReviewNotes { get; private set; }
    public bool? Preventable { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public static ComplicationCase Report(
        string caseRef, string patientRef, string unit, ComplicationType type, ComplicationSeverity severity,
        DateTimeOffset occurredDateUtc, string description, Guid? departmentId = null)
    {
        if (string.IsNullOrWhiteSpace(patientRef))
        {
            throw new DomainException("CMP-001", "A patient reference is required.");
        }

        if (occurredDateUtc == default)
        {
            throw new DomainException("CMP-002", "The date the complication occurred is required.");
        }

        return new ComplicationCase
        {
            CaseRef = caseRef,
            PatientRef = patientRef.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? "Unknown" : unit.Trim(),
            Type = type,
            Severity = severity,
            OccurredDateUtc = occurredDateUtc,
            Description = description?.Trim() ?? string.Empty,
            DepartmentId = departmentId,
            Status = ComplicationStatus.Reported,
        };
    }

    /// <summary>Records the peer review with a preventability judgement (Reported ⇒ Reviewed).</summary>
    public void RecordReview(Guid reviewerId, string notes, bool preventable, DateTimeOffset at)
    {
        if (Status != ComplicationStatus.Reported)
        {
            throw new InvalidStateTransitionException("CMP-010", $"Cannot review a case in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainException("CMP-011", "Review notes are required.");
        }

        ReviewedBy = reviewerId;
        ReviewNotes = notes.Trim();
        Preventable = preventable;
        ReviewedAtUtc = at;
        Status = ComplicationStatus.Reviewed;
    }

    /// <summary>Closes the case after review (Reviewed ⇒ Closed).</summary>
    public void Close()
    {
        if (Status != ComplicationStatus.Reviewed)
        {
            throw new InvalidStateTransitionException("CMP-012", "A case must be reviewed before it is closed.");
        }

        Status = ComplicationStatus.Closed;
    }

    public Guid? RejectedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }

    /// <summary>
    /// Rejects the case as not-a-complication (duplicate, wrong patient, or
    /// ruled out on peer review) — Reported/Reviewed ⇒ Rejected. The record
    /// stays for traceability but leaves the morbidity counts (M-18); a closed
    /// case is settled history and cannot be rejected.
    /// </summary>
    public void Reject(Guid reviewerId, string reason, DateTimeOffset at)
    {
        if (Status is ComplicationStatus.Closed or ComplicationStatus.Rejected)
        {
            throw new InvalidStateTransitionException("CMP-013", $"Cannot reject a case in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("CMP-014", "A rejection reason is required.");
        }

        RejectedBy = reviewerId;
        RejectionReason = reason.Trim();
        RejectedAtUtc = at;
        Status = ComplicationStatus.Rejected;
        Raise(new ComplicationCaseRejected(Id, CaseRef, Type.ToString()));
    }
}

public sealed record ComplicationCaseRejected(Guid ComplicationCaseId, string CaseRef, string Type) : DomainEvent;
