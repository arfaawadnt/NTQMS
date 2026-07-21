using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Operations;
using NT.QAMS.Domain.Records;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Records;

public sealed record ArchiveRecordCommand(
    string SourceModule, string SourceRef, Guid? SnapshotFileId, RetentionClass RetentionClass)
    : ICommand<Guid>;

public sealed class ArchiveRecordHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs, IClock clock)
    : ICommandHandler<ArchiveRecordCommand, Guid>
{
    public async Task<Guid> Handle(ArchiveRecordCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        // A source record is archivable once.
        if (await db.ArchiveEntries.AnyAsync(
                a => a.SourceModule == c.SourceModule && a.SourceRef == c.SourceRef, ct))
        {
            throw new DomainException("ARC-020", $"{c.SourceModule} {c.SourceRef} is already archived.");
        }

        if (c.SnapshotFileId is { } fileId && !await db.Files.AnyAsync(f => f.Id == fileId, ct))
        {
            throw new DomainException("FILE-404", "Snapshot file not found.");
        }

        var archiveRef = await refs.NextAsync(tenantId, "ARC", ct);
        var entry = ArchiveEntry.Archive(
            archiveRef, c.SourceModule, c.SourceRef, c.SnapshotFileId,
            c.RetentionClass, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), actor);

        db.ArchiveEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.Id;
    }
}

public sealed record RetrieveRecordCommand(Guid ArchiveId) : ICommand;
public sealed record ReturnRecordCommand(Guid ArchiveId) : ICommand;
public sealed record DisposeRecordCommand(Guid ArchiveId) : ICommand;

internal static class ArchiveLoader
{
    public static async Task<ArchiveEntry> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.ArchiveEntries.SingleOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new DomainException("ARC-404", "Archive entry not found.");
}

public sealed class RetrieveRecordHandler(IAppDbContext db) : ICommandHandler<RetrieveRecordCommand>
{
    public async Task Handle(RetrieveRecordCommand c, CancellationToken ct)
    {
        (await ArchiveLoader.LoadAsync(db, c.ArchiveId, ct)).Retrieve();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ReturnRecordHandler(IAppDbContext db) : ICommandHandler<ReturnRecordCommand>
{
    public async Task Handle(ReturnRecordCommand c, CancellationToken ct)
    {
        (await ArchiveLoader.LoadAsync(db, c.ArchiveId, ct)).Return();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DisposeRecordHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<DisposeRecordCommand>
{
    public async Task Handle(DisposeRecordCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        (await ArchiveLoader.LoadAsync(db, c.ArchiveId, ct))
            .AuthorizeDisposal(actor, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetArchivesQuery(string? State = null) : IQuery<IReadOnlyList<ArchiveListItemDto>>;

public sealed class GetArchivesHandler(IAppDbContext db)
    : IQueryHandler<GetArchivesQuery, IReadOnlyList<ArchiveListItemDto>>
{
    public async Task<IReadOnlyList<ArchiveListItemDto>> Handle(GetArchivesQuery q, CancellationToken ct)
    {
        var query = db.ArchiveEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State))
        {
            query = query.Where(a => a.State.ToString() == q.State);
        }

        return await query.OrderByDescending(a => a.ArchivedOn)
            .Take(500)
            .Select(a => new ArchiveListItemDto(
                a.Id, a.ArchiveRef, a.SourceModule, a.SourceRef,
                a.RetentionClass.ToString(), a.ArchivedOn, a.RetentionExpiry, a.State.ToString()))
            .ToListAsync(ct);
    }
}
