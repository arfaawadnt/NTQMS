using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AuditManagement;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AuditManagement.Queries;

public sealed record GetAuditsQuery(
    string? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<AuditListItemDto>>;

public sealed class GetAuditsHandler(IAppDbContext db)
    : IQueryHandler<GetAuditsQuery, Contracts.Common.PagedResponse<AuditListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<AuditListItemDto>> Handle(GetAuditsQuery q, CancellationToken ct)
    {
        var query = db.Audits.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(a => a.Status.ToString() == q.Status);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderByDescending(a => a.PlannedDate)
            .Select(a => new AuditListItemDto(
                a.Id, a.AuditRef, a.Title, a.Type.ToString(), a.Status.ToString(),
                a.LeadAuditorId, a.PlannedDate, a.CreatedAtUtc, a.BranchId, a.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetAuditByIdQuery(Guid AuditId) : IQuery<AuditDetailDto>;

public sealed class GetAuditByIdHandler(IAppDbContext db)
    : IQueryHandler<GetAuditByIdQuery, AuditDetailDto>
{
    public async Task<AuditDetailDto> Handle(GetAuditByIdQuery q, CancellationToken ct)
    {
        var audit = await db.Audits
            .AsNoTracking()
            .Include(a => a.Checklist)
            .Include(a => a.Findings)
            .SingleOrDefaultAsync(a => a.Id == q.AuditId, ct)
            ?? throw new DomainException("AUD-404", "Audit not found.");

        return new AuditDetailDto(
            audit.Id, audit.AuditRef, audit.Title, audit.Type.ToString(), audit.Status.ToString(),
            audit.LeadAuditorId, audit.PlannedDate, audit.SignedOffBy, audit.SignedOffAtUtc,
            audit.Checklist.Select(i => new ChecklistItemDto(
                i.Id, i.IsoClause, i.Question, i.Verdict.ToString(), i.Evidence)).ToList(),
            audit.Findings.Select(f => new FindingDto(
                f.Id, f.Grade.ToString(), f.Description, f.NcId)).ToList());
    }
}
