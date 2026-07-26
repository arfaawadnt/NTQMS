using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.DocumentControl;

public enum ControlledCopyStatus { Issued, Returned, Destroyed }

/// <summary>
/// A controlled printed-copy / distribution register entry (F-11 / ISO 17025 §8.3,
/// ISO 9001 §7.5.3): the record that a numbered physical copy of a published
/// controlled document was issued to a named holder, and its subsequent return or
/// destruction. Pinned to the version issued, so a copy of a superseded version is
/// visibly obsolete in the register. The register is the control that prevents
/// uncontrolled paper from circulating; issuing electronically-only means simply
/// not creating entries.
/// </summary>
public sealed class DocumentControlledCopy : AggregateRoot, ITenantScoped
{
    private DocumentControlledCopy()
    {
        DocumentCode = null!;
        VersionLabel = null!;
        Holder = null!;
    }

    public Guid TenantId { get; set; }
    public Guid DocumentId { get; private set; }
    public string DocumentCode { get; private set; }
    public string VersionLabel { get; private set; }
    /// <summary>Sequential copy number within the document, stamped at issue.</summary>
    public int CopyNumber { get; private set; }
    /// <summary>Who holds the copy — a person, role, or physical location.</summary>
    public string Holder { get; private set; }
    public Guid IssuedBy { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public ControlledCopyStatus Status { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public static DocumentControlledCopy Issue(
        Guid documentId, string documentCode, string versionLabel, int copyNumber,
        string holder, Guid issuedBy, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(holder))
        {
            throw new DomainException("CCP-001", "A copy holder (person, role, or location) is required.");
        }

        if (copyNumber < 1)
        {
            throw new DomainException("CCP-002", "The copy number must be positive.");
        }

        return new DocumentControlledCopy
        {
            DocumentId = documentId,
            DocumentCode = documentCode,
            VersionLabel = versionLabel,
            CopyNumber = copyNumber,
            Holder = holder.Trim(),
            IssuedBy = issuedBy,
            IssuedAtUtc = at,
            Status = ControlledCopyStatus.Issued,
        };
    }

    /// <summary>Record the copy's return or destruction, closing the register entry.</summary>
    public void Close(ControlledCopyStatus outcome, Guid actorId, DateTimeOffset at)
    {
        if (outcome is not (ControlledCopyStatus.Returned or ControlledCopyStatus.Destroyed))
        {
            throw new DomainException("CCP-003", "A controlled copy can only be closed as Returned or Destroyed.");
        }

        if (Status != ControlledCopyStatus.Issued)
        {
            throw new InvalidStateTransitionException(
                "CCP-010", $"Only an issued copy can be returned or destroyed (current: {Status}).");
        }

        Status = outcome;
        ClosedBy = actorId;
        ClosedAtUtc = at;
        Raise(new ControlledCopyClosed(Id, DocumentId, DocumentCode, CopyNumber, outcome.ToString(), TenantId));
    }
}

public sealed record ControlledCopyClosed(
    Guid CopyId, Guid DocumentId, string DocumentCode, int CopyNumber, string Outcome, Guid TenantId) : DomainEvent;
