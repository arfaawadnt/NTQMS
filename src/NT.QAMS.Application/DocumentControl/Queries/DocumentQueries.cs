using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.DocumentControl;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.DocumentControl.Queries;

public sealed record GetDocumentsQuery(string? Status = null, string? Search = null)
    : IQuery<IReadOnlyList<DocumentListItemDto>>;

public sealed class GetDocumentsHandler(IAppDbContext db)
    : IQueryHandler<GetDocumentsQuery, IReadOnlyList<DocumentListItemDto>>
{
    public async Task<IReadOnlyList<DocumentListItemDto>> Handle(GetDocumentsQuery q, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(d => d.Status.ToString() == q.Status);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(d => d.Title.Contains(term) || d.Code.Contains(term));
        }

        return await query
            .OrderBy(d => d.Code)
            .Take(500)
            .Select(d => new DocumentListItemDto(
                d.Id, d.Code, d.Title, d.Category, d.Status.ToString(),
                d.Versions
                    .Where(v => v.State == VersionState.Published)
                    .Select(v => v.Major + "." + v.Minor)
                    .FirstOrDefault(),
                d.CreatedAtUtc))
            .ToListAsync(ct);
    }
}

public sealed record GetDocumentByIdQuery(Guid DocumentId) : IQuery<DocumentDetailDto>;

public sealed class GetDocumentByIdHandler(IAppDbContext db)
    : IQueryHandler<GetDocumentByIdQuery, DocumentDetailDto>
{
    public async Task<DocumentDetailDto> Handle(GetDocumentByIdQuery q, CancellationToken ct)
    {
        var doc = await db.Documents
            .AsNoTracking()
            .Include(d => d.Versions)
            .SingleOrDefaultAsync(d => d.Id == q.DocumentId, ct)
            ?? throw new DomainException("DOC-404", "Document not found.");

        return new DocumentDetailDto(
            doc.Id, doc.Code, doc.Title, doc.Category, doc.Status.ToString(), doc.CreatedAtUtc,
            doc.Versions
                .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor)
                .Select(v => new DocumentVersionDto(
                    v.Id, v.VersionLabel, v.State.ToString(), v.FileId, v.ChangeSummary,
                    v.AuthorId, v.RecommendedBy, v.RecommendedAtUtc,
                    v.ApprovedBy, v.ApprovedAtUtc, v.RejectionReason))
                .ToList());
    }
}
