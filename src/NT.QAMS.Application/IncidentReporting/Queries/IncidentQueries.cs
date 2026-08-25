using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.IncidentReporting.Commands;
using NT.QAMS.Contracts.IncidentReporting;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IncidentReporting.Queries;

public sealed record GetIncidentsQuery(
    string? Status = null, string? Search = null, string? Category = null, bool SentinelOnly = false,
    int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<IncidentListItemDto>>;

public sealed class GetIncidentsHandler(IAppDbContext db)
    : IQueryHandler<GetIncidentsQuery, Contracts.Common.PagedResponse<IncidentListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<IncidentListItemDto>> Handle(
        GetIncidentsQuery q, CancellationToken ct)
    {
        var query = db.Incidents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(i => i.Status.ToString() == q.Status);
        }

        if (!string.IsNullOrWhiteSpace(q.Category)
            && Enum.TryParse<IncidentCategory>(q.Category, ignoreCase: true, out var category))
        {
            query = query.Where(i => i.Category == category);
        }

        if (q.SentinelOnly)
        {
            query = query.Where(i => i.IsSentinel);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(i => i.Title.Contains(term) || i.IncidentRef.Contains(term));
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new IncidentListItemDto(
                i.Id, i.IncidentRef, i.Title, i.Status.ToString(), i.Category.ToString(),
                i.HarmGrade.ToString(), i.IsSentinel, i.IsAnonymous, i.OccurredAtUtc, i.CreatedAtUtc,
                i.BranchId, i.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetIncidentByIdQuery(Guid IncidentId) : IQuery<IncidentDetailDto>;

public sealed class GetIncidentByIdHandler(IAppDbContext db) : IQueryHandler<GetIncidentByIdQuery, IncidentDetailDto>
{
    public async Task<IncidentDetailDto> Handle(GetIncidentByIdQuery q, CancellationToken ct)
    {
        var i = await db.Incidents
            .AsNoTracking()
            .Include(x => x.ContributingFactors)
            .Include(x => x.Timeline)
            .SingleOrDefaultAsync(x => x.Id == q.IncidentId, ct)
            ?? throw new DomainException("INC-404", "Incident not found.");

        return new IncidentDetailDto(
            i.Id, i.IncidentRef, i.Title, i.Description, i.Status.ToString(),
            i.Category.ToString(), i.Location, i.HarmGrade.ToString(), i.Channel.ToString(),
            i.IsSentinel, i.SentinelDeclaredAtUtc, i.IsAnonymous, i.ReportedBy, i.AssignedTo, i.InvestigatorId,
            i.InvestigationSummary, i.RejectionReason, i.ClosureSummary, i.CorrectiveActionNcId,
            i.OccurredAtUtc, i.CreatedAtUtc,
            i.ContributingFactors
                .Select(f => new ContributingFactorDto(f.Id, f.Category.ToString(), f.Description)).ToList(),
            i.Timeline
                .OrderBy(t => t.OccurredAtUtc)
                .Select(t => new IncidentTimelineEntryDto(t.Id, t.OccurredAtUtc, t.Note, t.RecordedBy)).ToList());
    }
}

/// <summary>
/// Tracks an anonymous report by its one-time follow-up reference. Returns status only —
/// never the incident body — and matches on the stored hash, so knowing the reference
/// reveals progress without exposing the report or its handling to the caller.
/// </summary>
public sealed record TrackAnonymousIncidentQuery(string FollowUpReference) : IQuery<IncidentTrackingDto>;

public sealed class TrackAnonymousIncidentHandler(IAppDbContext db)
    : IQueryHandler<TrackAnonymousIncidentQuery, IncidentTrackingDto>
{
    public async Task<IncidentTrackingDto> Handle(TrackAnonymousIncidentQuery q, CancellationToken ct)
    {
        var hash = AnonymousReference.Hash(q.FollowUpReference ?? string.Empty);
        var i = await db.Incidents
            .AsNoTracking()
            .Where(x => x.IsAnonymous && x.AnonymousReferenceHash == hash)
            .Select(x => new IncidentTrackingDto(x.IncidentRef, x.Status.ToString(), x.IsSentinel))
            .SingleOrDefaultAsync(ct)
            ?? throw new DomainException("INC-405", "No report matches that reference.");

        return i;
    }
}
