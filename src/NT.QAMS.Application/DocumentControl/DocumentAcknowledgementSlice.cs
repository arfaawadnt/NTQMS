using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.DocumentControl;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.DocumentControl;

// â”€â”€ Command â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>The current user confirms they have read and understood the document's published version.</summary>
[RequireInternalActor]
public sealed record AcknowledgeDocumentCommand(Guid DocumentId) : ICommand<Guid>;

public sealed class AcknowledgeDocumentHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<AcknowledgeDocumentCommand, Guid>
{
    public async Task<Guid> Handle(AcknowledgeDocumentCommand c, CancellationToken ct)
    {
        var userId = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var document = await db.Documents.Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == c.DocumentId, ct)
            ?? throw new DomainException("DOC-404", "Document not found.");

        var published = document.PublishedVersion
            ?? throw new DomainException("ACK-010", "Only a published document can be acknowledged.");

        // Idempotent: re-acknowledging the same version returns the existing receipt.
        var existing = await db.DocumentAcknowledgements
            .Where(a => a.DocumentId == document.Id
                && a.VersionLabel == published.VersionLabel
                && a.UserId == userId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is { } id)
        {
            return id;
        }

        var ack = DocumentAcknowledgement.Record(
            document.Id, document.Code, published.VersionLabel, userId, clock.UtcNow);
        db.DocumentAcknowledgements.Add(ack);
        await db.SaveChangesAsync(ct);
        return ack.Id;
    }
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Whether the current user has acknowledged this document's current published version.</summary>
public sealed record GetMyDocumentAcknowledgementQuery(Guid DocumentId)
    : IQuery<MyDocumentAcknowledgementDto>;

public sealed class GetMyDocumentAcknowledgementHandler(IAppDbContext db, ICurrentUser user)
    : IQueryHandler<GetMyDocumentAcknowledgementQuery, MyDocumentAcknowledgementDto>
{
    public async Task<MyDocumentAcknowledgementDto> Handle(
        GetMyDocumentAcknowledgementQuery q, CancellationToken ct)
    {
        var userId = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var document = await db.Documents.AsNoTracking().Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == q.DocumentId, ct)
            ?? throw new DomainException("DOC-404", "Document not found.");

        var publishedLabel = document.PublishedVersion?.VersionLabel;
        if (publishedLabel is null)
        {
            return new MyDocumentAcknowledgementDto(null, false, null);
        }

        var ack = await db.DocumentAcknowledgements.AsNoTracking()
            .Where(a => a.DocumentId == document.Id
                && a.VersionLabel == publishedLabel
                && a.UserId == userId)
            .Select(a => (DateTimeOffset?)a.AcknowledgedAtUtc)
            .FirstOrDefaultAsync(ct);

        return new MyDocumentAcknowledgementDto(publishedLabel, ack is not null, ack);
    }
}

/// <summary>All acknowledgement receipts for a document (quality-management view of coverage).</summary>
public sealed record GetDocumentAcknowledgementsQuery(Guid DocumentId)
    : IQuery<IReadOnlyList<DocumentAcknowledgementDto>>;

public sealed class GetDocumentAcknowledgementsHandler(IAppDbContext db)
    : IQueryHandler<GetDocumentAcknowledgementsQuery, IReadOnlyList<DocumentAcknowledgementDto>>
{
    public async Task<IReadOnlyList<DocumentAcknowledgementDto>> Handle(
        GetDocumentAcknowledgementsQuery q, CancellationToken ct)
    {
        var acks = await db.DocumentAcknowledgements.AsNoTracking()
            .Where(a => a.DocumentId == q.DocumentId)
            .OrderByDescending(a => a.AcknowledgedAtUtc)
            .Select(a => new { a.UserId, a.VersionLabel, a.AcknowledgedAtUtc })
            .ToListAsync(ct);

        var userIds = acks.Select(a => a.UserId).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return acks
            .Select(a => new DocumentAcknowledgementDto(
                a.UserId, names.TryGetValue(a.UserId, out var name) ? name : "(unknown)",
                a.VersionLabel, a.AcknowledgedAtUtc))
            .ToList();
    }
}
