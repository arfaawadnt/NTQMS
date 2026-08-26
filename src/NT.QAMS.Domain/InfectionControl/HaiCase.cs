using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.InfectionControl;

/// <summary>
/// The healthcare-associated infection types surveilled. The three device-associated types map to
/// a <see cref="DeviceType"/> (their rate denominator); a surgical-site infection (Ssi) is not
/// device-associated and is counted rather than rated per device-day.
/// </summary>
public enum HaiType { Clabsi, Cauti, Vap, Ssi }

/// <summary>Lifecycle of an HAI case.</summary>
public enum HaiStatus { Reported, Reviewed, Closed }

/// <summary>
/// A healthcare-associated infection case (HQMS M09): a CLABSI, CAUTI, VAP or SSI. The case is
/// reported, reviewed by infection control, then closed. Device-associated cases feed the
/// per-1,000-device-day rate; the causative organism is captured for the antibiogram/surveillance.
/// </summary>
public sealed class HaiCase : AggregateRoot, ITenantScoped
{
    private HaiCase()
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
    public HaiType Type { get; private set; }
    public string PatientRef { get; private set; }
    public string Unit { get; private set; }
    public DateTimeOffset OnsetDateUtc { get; private set; }

    /// <summary>Causative organism, when identified (free text — the lab result is the record of truth).</summary>
    public string? Organism { get; private set; }

    public string Description { get; private set; }
    public HaiStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? ReviewNotes { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public static HaiCase Report(
        string caseRef, HaiType type, string patientRef, string unit, DateTimeOffset onsetDateUtc,
        string? organism, string description, Guid? departmentId = null)
    {
        if (string.IsNullOrWhiteSpace(patientRef))
        {
            throw new DomainException("HAI-001", "A patient reference is required.");
        }

        if (onsetDateUtc == default)
        {
            throw new DomainException("HAI-002", "The infection onset date is required.");
        }

        return new HaiCase
        {
            CaseRef = caseRef,
            Type = type,
            PatientRef = patientRef.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? "Unknown" : unit.Trim(),
            OnsetDateUtc = onsetDateUtc,
            Organism = string.IsNullOrWhiteSpace(organism) ? null : organism.Trim(),
            Description = description?.Trim() ?? string.Empty,
            DepartmentId = departmentId,
            Status = HaiStatus.Reported,
        };
    }

    /// <summary>Records the infection-control review (Reported ⇒ Reviewed).</summary>
    public void RecordReview(Guid reviewerId, string notes, DateTimeOffset at)
    {
        if (Status != HaiStatus.Reported)
        {
            throw new InvalidStateTransitionException("HAI-010", $"Cannot review a case in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainException("HAI-011", "Review notes are required.");
        }

        ReviewedBy = reviewerId;
        ReviewNotes = notes.Trim();
        ReviewedAtUtc = at;
        Status = HaiStatus.Reviewed;
        Raise(new HaiCaseReviewed(Id, CaseRef, Type.ToString()));
    }

    /// <summary>Closes the case after review (Reviewed ⇒ Closed).</summary>
    public void Close()
    {
        if (Status != HaiStatus.Reviewed)
        {
            throw new InvalidStateTransitionException("HAI-012", "A case must be reviewed before it is closed.");
        }

        Status = HaiStatus.Closed;
    }

    /// <summary>The device whose device-days form this case's rate denominator, or null for SSI.</summary>
    public DeviceType? AssociatedDevice => Type switch
    {
        HaiType.Clabsi => DeviceType.CentralLine,
        HaiType.Cauti => DeviceType.UrinaryCatheter,
        HaiType.Vap => DeviceType.Ventilator,
        _ => null,
    };
}

public sealed record HaiCaseReviewed(Guid HaiCaseId, string CaseRef, string Type) : DomainEvent;
