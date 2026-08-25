using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Accreditation;

/// <summary>The kind of record attached as evidence to a measurable element.</summary>
public enum EvidenceSourceType { Document, Incident, Nonconformance, Audit, Indicator, Training, Committee, Other }

/// <summary>
/// A piece of evidence attached to a measurable element (HQMS M07). The link is
/// polymorphic and loose — it holds the source's type, id and human reference rather
/// than a foreign key — so any record in the system (a controlled document, an incident,
/// an audit, an indicator, a training record, a committee minute) can serve as evidence
/// for one or more elements, and the same item can be reused across standards.
/// </summary>
public sealed class EvidenceLink : AggregateRoot, ITenantScoped
{
    private EvidenceLink()
    {
        SourceRef = null!;
    }

    public Guid TenantId { get; set; }
    public Guid StandardSetId { get; private set; }
    public Guid ElementId { get; private set; }
    public EvidenceSourceType SourceType { get; private set; }

    /// <summary>Id of the source record (loose cross-aggregate reference, not an FK).</summary>
    public Guid SourceId { get; private set; }

    /// <summary>Human-readable reference to the source (e.g. "SOP-CAL-1 v2.0", "INC-2026-0007").</summary>
    public string SourceRef { get; private set; }

    public string? Description { get; private set; }
    public Guid LinkedBy { get; private set; }
    public DateTimeOffset LinkedAtUtc { get; private set; }

    public static EvidenceLink Create(
        Guid standardSetId, Guid elementId, EvidenceSourceType sourceType, Guid sourceId,
        string sourceRef, string? description, Guid linkedBy, DateTimeOffset at)
    {
        if (standardSetId == Guid.Empty || elementId == Guid.Empty)
        {
            throw new DomainException("EVD-001", "A standard set and element are required.");
        }

        if (string.IsNullOrWhiteSpace(sourceRef))
        {
            throw new DomainException("EVD-002", "A source reference is required.");
        }

        return new EvidenceLink
        {
            StandardSetId = standardSetId,
            ElementId = elementId,
            SourceType = sourceType,
            SourceId = sourceId,
            SourceRef = sourceRef.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            LinkedBy = linkedBy,
            LinkedAtUtc = at,
        };
    }
}

public sealed record EvidenceLinked(
    Guid EvidenceId, Guid StandardSetId, Guid ElementId, string SourceRef) : DomainEvent;
