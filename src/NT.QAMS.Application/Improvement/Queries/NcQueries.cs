using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Improvement;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Improvement.Queries;

public sealed record GetNcsQuery(
    string? Status = null, string? Search = null, string? EventType = null,
    int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<NcListItemDto>>;

public sealed class GetNcsHandler(IAppDbContext db)
    : IQueryHandler<GetNcsQuery, Contracts.Common.PagedResponse<NcListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<NcListItemDto>> Handle(GetNcsQuery q, CancellationToken ct)
    {
        var query = db.Nonconformances.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(n => n.Status.ToString() == q.Status);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.NcRef.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(q.EventType)
            && Enum.TryParse<QualityEventType>(q.EventType, ignoreCase: true, out var eventType))
        {
            query = query.Where(n => n.EventType == eventType);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NcListItemDto(
                n.Id, n.NcRef, n.Title, n.Status.ToString(), n.Severity, n.Rpn,
                n.SourceType.ToString(), n.CreatedAtUtc, n.EventType.ToString(), n.BranchId, n.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetNcByIdQuery(Guid NcId) : IQuery<NcDetailDto>;

public sealed class GetNcByIdHandler(IAppDbContext db) : IQueryHandler<GetNcByIdQuery, NcDetailDto>
{
    public async Task<NcDetailDto> Handle(GetNcByIdQuery q, CancellationToken ct)
    {
        var nc = await db.Nonconformances
            .AsNoTracking()
            .Include(n => n.CapaActions)
            .Include(n => n.RcaRecords)
            .SingleOrDefaultAsync(n => n.Id == q.NcId, ct)
            ?? throw new DomainException("NC-404", "Nonconformance not found.");

        return new NcDetailDto(
            nc.Id, nc.NcRef, nc.Title, nc.Description, nc.Status.ToString(),
            nc.Severity, nc.Likelihood, nc.Rpn, nc.SourceType.ToString(), nc.EventType.ToString(),
            nc.RaisedBy, nc.AssignedTo, nc.RejectionReason, nc.CreatedAtUtc,
            nc.CapaActions.Select(a => new CapaActionDto(
                a.Id, a.Type.ToString(), a.Details, a.OwnerId, a.DueDate,
                a.Status.ToString(), a.CompletedAtUtc)).ToList(),
            nc.RcaRecords.Select(r => new RcaRecordDto(
                r.Id, r.Method.ToString(), r.Analysis, r.InvestigatorId)).ToList());
    }
}
