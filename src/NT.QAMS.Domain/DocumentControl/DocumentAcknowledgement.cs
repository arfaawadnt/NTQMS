using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.DocumentControl;

/// <summary>
/// A read-and-understand receipt (F-11 / ISO 9001 §7.5, ISO 17025 §8.3, 21 CFR
/// Part 11 training): the durable evidence that a named person confirmed they read
/// and understood a specific published version of a controlled document. Append-only
/// and pinned to the exact version label, so revising the document re-opens the
/// acknowledgement — a prior receipt never covers a newer version.
/// </summary>
public sealed class DocumentAcknowledgement : AggregateRoot, ITenantScoped
{
    private DocumentAcknowledgement()
    {
        DocumentCode = null!;
        VersionLabel = null!;
    }

    public Guid TenantId { get; set; }
    public Guid DocumentId { get; private set; }
    public string DocumentCode { get; private set; }
    public string VersionLabel { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset AcknowledgedAtUtc { get; private set; }

    public static DocumentAcknowledgement Record(
        Guid documentId, string documentCode, string versionLabel, Guid userId, DateTimeOffset at)
    {
        if (documentId == Guid.Empty)
        {
            throw new DomainException("ACK-001", "A document is required to record an acknowledgement.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("ACK-002", "An acknowledging user is required.");
        }

        if (string.IsNullOrWhiteSpace(versionLabel))
        {
            throw new DomainException("ACK-003", "A published version is required to acknowledge.");
        }

        var ack = new DocumentAcknowledgement
        {
            DocumentId = documentId,
            DocumentCode = documentCode,
            VersionLabel = versionLabel,
            UserId = userId,
            AcknowledgedAtUtc = at,
        };
        ack.Raise(new DocumentAcknowledged(documentId, documentCode, versionLabel, userId));
        return ack;
    }
}

public sealed record DocumentAcknowledged(
    Guid DocumentId, string DocumentCode, string VersionLabel, Guid UserId) : DomainEvent;
