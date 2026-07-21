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
    public Guid? SnapshotFileId { get; private set; }
    public RetentionClass RetentionClass { get; private set; }
    public DateOnly ArchivedOn { get; private set; }
    public DateOnly? RetentionExpiry { get; private set; }
    public ArchiveState State { get; private set; }
    public Guid ArchivedBy { get; private set; }
    public Guid? DisposalAuthorizedBy { get; private set; }

    public static ArchiveEntry Archive(
        string archiveRef, string sourceModule, string sourceRef, Guid? snapshotFileId,
        RetentionClass retentionClass, DateOnly archivedOn, Guid archivedBy)
    {
        if (string.IsNullOrWhiteSpace(sourceModule) || string.IsNullOrWhiteSpace(sourceRef))
        {
            throw new DomainException("ARC-001", "Source module and reference are required.");
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
