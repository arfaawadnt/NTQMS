using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Records;

public enum RetentionClass { FiveYears, TenYears, Permanent }

public enum ArchiveState { Archived, Retrieved, Disposed }

/// <summary>
/// A quality-archive entry: a snapshot reference to a source record with a
/// retention class. Disposal is only permitted after the retention period and
/// only with an authorization signature (ISO 17025 8.4.2). Permanent-class
/// records can never be disposed.
/// </summary>
public sealed class ArchiveEntry : AggregateRoot, ITenantScoped
{
    private ArchiveEntry()
    {
        ArchiveRef = null!;
        SourceModule = null!;
        SourceRef = null!;
    }

    public Guid TenantId { get; set; }
    public string ArchiveRef { get; private set; }
    public string SourceModule { get; private set; }
    public string SourceRef { get; private set; }
    /// <summary>
    /// The immutable content snapshot stored for this archive entry. Required
    /// (F-14 / Part 11 §11.10(c)) — an archive with no content copy is not an
    /// archive. Physically nullable in the schema for backward compatibility with
    /// pre-F-14 rows, but the aggregate never creates an entry without one.
    /// </summary>
    public Guid? SnapshotFileId { get; private set; }
    public RetentionClass RetentionClass { get; private set; }
    public DateOnly ArchivedOn { get; private set; }
    public DateOnly? RetentionExpiry { get; private set; }
    public ArchiveState State { get; private set; }
    public Guid ArchivedBy { get; private set; }
    public Guid? DisposalAuthorizedBy { get; private set; }

    /// <summary>When true, disposal is blocked regardless of retention expiry (litigation / investigation hold).</summary>
    public bool IsOnLegalHold { get; private set; }
    public string? LegalHoldReason { get; private set; }
    public Guid? LegalHoldPlacedBy { get; private set; }

    public static ArchiveEntry Archive(
        string archiveRef, string sourceModule, string sourceRef, Guid snapshotFileId,
        RetentionClass retentionClass, DateOnly archivedOn, Guid archivedBy)
    {
        if (string.IsNullOrWhiteSpace(sourceModule) || string.IsNullOrWhiteSpace(sourceRef))
        {
            throw new DomainException("ARC-001", "Source module and reference are required.");
        }

        if (snapshotFileId == Guid.Empty)
        {
            throw new DomainException("ARC-002", "An immutable content snapshot is required to archive a record.");
        }

        return new ArchiveEntry
        {
            ArchiveRef = archiveRef,
            SourceModule = sourceModule.Trim(),
            SourceRef = sourceRef.Trim(),
            SnapshotFileId = snapshotFileId,
            RetentionClass = retentionClass,
            ArchivedOn = archivedOn,
            RetentionExpiry = ExpiryFor(retentionClass, archivedOn),
            State = ArchiveState.Archived,
            ArchivedBy = archivedBy,
        };
    }

    /// <summary>Place the record under legal hold — disposal is refused until the hold is released.</summary>
    public void PlaceLegalHold(string reason, Guid actorId)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("ARC-030", "A reason is required to place a legal hold.");
        }

        if (State == ArchiveState.Disposed)
        {
            throw new InvalidStateTransitionException("ARC-031", "A disposed record cannot be placed on legal hold.");
        }

        IsOnLegalHold = true;
        LegalHoldReason = reason.Trim();
        LegalHoldPlacedBy = actorId;
        Raise(new ArchiveLegalHoldPlaced(Id, ArchiveRef, LegalHoldReason, actorId, TenantId));
    }

    /// <summary>Release a legal hold once the litigation/investigation reason no longer applies.</summary>
    public void ReleaseLegalHold(Guid actorId)
    {
        if (!IsOnLegalHold)
        {
            throw new InvalidStateTransitionException("ARC-032", "The record is not on legal hold.");
        }

        IsOnLegalHold = false;
        LegalHoldReason = null;
        LegalHoldPlacedBy = null;
        Raise(new ArchiveLegalHoldReleased(Id, ArchiveRef, actorId, TenantId));
    }

    public void Retrieve()
    {
        if (State == ArchiveState.Disposed)
        {
            throw new InvalidStateTransitionException("ARC-010", "A disposed record cannot be retrieved.");
        }

        State = ArchiveState.Retrieved;
    }

    public void Return()
    {
        if (State != ArchiveState.Retrieved)
        {
            throw new InvalidStateTransitionException("ARC-011", "Only a retrieved record can be returned.");
        }

        State = ArchiveState.Archived;
    }

    public void AuthorizeDisposal(Guid actorId, DateOnly asOf)
    {
        if (State == ArchiveState.Disposed)
        {
            throw new InvalidStateTransitionException("ARC-012", "Record is already disposed.");
        }

        if (IsOnLegalHold)
        {
            throw new DomainException(
                "ARC-015", "The record is under legal hold and cannot be disposed until the hold is released.");
        }

        if (RetentionClass == RetentionClass.Permanent || RetentionExpiry is null)
        {
            throw new DomainException("ARC-013", "Permanent-retention records cannot be disposed.");
        }

        if (RetentionExpiry.Value > asOf)
        {
            throw new DomainException(
                "ARC-014", $"Retention period runs until {RetentionExpiry:yyyy-MM-dd}; disposal is not yet permitted.");
        }

        State = ArchiveState.Disposed;
        DisposalAuthorizedBy = actorId;
        Raise(new RecordDisposed(Id, ArchiveRef, SourceModule, SourceRef, actorId, TenantId));
    }

    private static DateOnly? ExpiryFor(RetentionClass retentionClass, DateOnly archivedOn) => retentionClass switch
    {
        RetentionClass.FiveYears => archivedOn.AddYears(5),
        RetentionClass.TenYears => archivedOn.AddYears(10),
        _ => null,
    };
}

public sealed record RecordDisposed(
    Guid ArchiveId, string ArchiveRef, string SourceModule, string SourceRef, Guid AuthorizedBy, Guid TenantId)
    : DomainEvent;

public sealed record ArchiveLegalHoldPlaced(
    Guid ArchiveId, string ArchiveRef, string Reason, Guid PlacedBy, Guid TenantId) : DomainEvent;

public sealed record ArchiveLegalHoldReleased(
    Guid ArchiveId, string ArchiveRef, Guid ReleasedBy, Guid TenantId) : DomainEvent;
