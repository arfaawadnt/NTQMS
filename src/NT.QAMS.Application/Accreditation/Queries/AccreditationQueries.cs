using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Accreditation;
using NT.QAMS.Domain.Accreditation;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Accreditation.Queries;

public sealed record GetStandardSetsQuery(string? Status = null) : IQuery<IReadOnlyList<StandardSetListItemDto>>;

public sealed class GetStandardSetsHandler(IAppDbContext db)
    : IQueryHandler<GetStandardSetsQuery, IReadOnlyList<StandardSetListItemDto>>
{
    public async Task<IReadOnlyList<StandardSetListItemDto>> Handle(GetStandardSetsQuery q, CancellationToken ct)
    {
        var query = db.StandardSets.AsNoTracking().Include(s => s.Elements).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(s => s.Status.ToString() == q.Status);
        }

        var sets = await query.OrderBy(s => s.Framework).ThenBy(s => s.Name).ToListAsync(ct);
        return sets
            .Select(s => new StandardSetListItemDto(
                s.Id, s.Framework.ToString(), s.Name, s.Version, s.Status.ToString(),
                s.Elements.Count, AccreditationReadiness.Overall(s.Elements).CompliancePercent))
            .ToList();
    }
}

public sealed record GetStandardSetByIdQuery(Guid StandardSetId) : IQuery<StandardSetDetailDto>;

public sealed class GetStandardSetByIdHandler(IAppDbContext db)
    : IQueryHandler<GetStandardSetByIdQuery, StandardSetDetailDto>
{
    public async Task<StandardSetDetailDto> Handle(GetStandardSetByIdQuery q, CancellationToken ct)
    {
        var set = await db.StandardSets.AsNoTracking().Include(s => s.Elements)
            .SingleOrDefaultAsync(s => s.Id == q.StandardSetId, ct)
            ?? throw new DomainException("STD-404", "Standard set not found.");

        var evidenceCounts = await EvidenceCountsAsync(db, q.StandardSetId, ct);

        var elements = set.Elements
            .OrderBy(e => e.ChapterCode).ThenBy(e => e.ElementCode)
            .Select(e => new StandardElementDto(
                e.Id, e.ChapterCode, e.ChapterTitle, e.StandardCode, e.ElementCode, e.Text, e.Weight,
                e.ComplianceStatus.ToString(), e.AssessmentNote, e.AssessedBy, e.AssessedAtUtc,
                evidenceCounts.TryGetValue(e.Id, out var n) ? n : 0))
            .ToList();

        return new StandardSetDetailDto(
            set.Id, set.Framework.ToString(), set.Name, set.Version, set.Status.ToString(), elements);
    }

    internal static async Task<Dictionary<Guid, int>> EvidenceCountsAsync(
        IAppDbContext db, Guid standardSetId, CancellationToken ct) =>
        (await db.EvidenceLinks.AsNoTracking()
            .Where(l => l.StandardSetId == standardSetId)
            .GroupBy(l => l.ElementId)
            .Select(g => new { ElementId = g.Key, Count = g.Count() })
            .ToListAsync(ct))
        .ToDictionary(x => x.ElementId, x => x.Count);
}

public sealed record GetReadinessDashboardQuery(Guid StandardSetId) : IQuery<ReadinessDashboardDto>;

public sealed class GetReadinessDashboardHandler(IAppDbContext db)
    : IQueryHandler<GetReadinessDashboardQuery, ReadinessDashboardDto>
{
    public async Task<ReadinessDashboardDto> Handle(GetReadinessDashboardQuery q, CancellationToken ct)
    {
        var set = await db.StandardSets.AsNoTracking().Include(s => s.Elements)
            .SingleOrDefaultAsync(s => s.Id == q.StandardSetId, ct)
            ?? throw new DomainException("STD-404", "Standard set not found.");

        return new ReadinessDashboardDto(
            set.Id, set.Framework.ToString(), set.Name, set.Version, set.Status.ToString(),
            Map(AccreditationReadiness.Overall(set.Elements)),
            AccreditationReadiness.ByChapter(set.Elements).Select(Map).ToList());
    }

    private static ReadinessScoreDto Map(ReadinessScore r) => new(
        r.ChapterCode, r.ChapterTitle, r.ElementCount, r.ApplicableCount, r.CompliantCount, r.PartialCount,
        r.NonCompliantCount, r.NotAssessedCount, r.NotApplicableCount, r.CompliancePercent);
}

/// <summary>
/// Ranks the elements that need attention: any element with no evidence, that is not
/// assessed, or assessed partially/non-compliant. Ordered by weight then severity so the
/// biggest readiness gains surface first — the prioritised gap list M07 requires.
/// </summary>
public sealed record GetGapAnalysisQuery(Guid StandardSetId) : IQuery<IReadOnlyList<GapItemDto>>;

public sealed class GetGapAnalysisHandler(IAppDbContext db)
    : IQueryHandler<GetGapAnalysisQuery, IReadOnlyList<GapItemDto>>
{
    public async Task<IReadOnlyList<GapItemDto>> Handle(GetGapAnalysisQuery q, CancellationToken ct)
    {
        var set = await db.StandardSets.AsNoTracking().Include(s => s.Elements)
            .SingleOrDefaultAsync(s => s.Id == q.StandardSetId, ct)
            ?? throw new DomainException("STD-404", "Standard set not found.");

        var evidenceCounts = await GetStandardSetByIdHandler.EvidenceCountsAsync(db, q.StandardSetId, ct);

        var gaps = new List<(GapItemDto Item, int Severity)>();
        foreach (var e in set.Elements)
        {
            if (e.ComplianceStatus == ComplianceStatus.NotApplicable)
            {
                continue;
            }

            var evidence = evidenceCounts.TryGetValue(e.Id, out var n) ? n : 0;
            var reasons = new List<string>();
            var severity = 0;

            if (e.ComplianceStatus == ComplianceStatus.NonCompliant) { reasons.Add("Non-compliant"); severity = 3; }
            else if (e.ComplianceStatus == ComplianceStatus.NotAssessed) { reasons.Add("Not assessed"); severity = Math.Max(severity, 2); }
            else if (e.ComplianceStatus == ComplianceStatus.PartiallyCompliant) { reasons.Add("Partially compliant"); severity = Math.Max(severity, 1); }

            if (evidence == 0) { reasons.Add("No evidence"); severity = Math.Max(severity, 2); }

            if (reasons.Count == 0)
            {
                continue;
            }

            gaps.Add((new GapItemDto(
                e.Id, e.ChapterCode, e.StandardCode, e.ElementCode, e.Text, e.Weight,
                e.ComplianceStatus.ToString(), evidence, string.Join("; ", reasons)), severity));
        }

        return gaps
            .OrderByDescending(g => g.Severity)
            .ThenByDescending(g => g.Item.Weight)
            .ThenBy(g => g.Item.ElementCode)
            .Select(g => g.Item)
            .ToList();
    }
}

public sealed record GetElementEvidenceQuery(Guid ElementId) : IQuery<IReadOnlyList<EvidenceLinkDto>>;

public sealed class GetElementEvidenceHandler(IAppDbContext db)
    : IQueryHandler<GetElementEvidenceQuery, IReadOnlyList<EvidenceLinkDto>>
{
    public async Task<IReadOnlyList<EvidenceLinkDto>> Handle(GetElementEvidenceQuery q, CancellationToken ct) =>
        await db.EvidenceLinks.AsNoTracking()
            .Where(l => l.ElementId == q.ElementId)
            .OrderByDescending(l => l.LinkedAtUtc)
            .Select(l => new EvidenceLinkDto(
                l.Id, l.ElementId, l.SourceType.ToString(), l.SourceId, l.SourceRef,
                l.Description, l.LinkedBy, l.LinkedAtUtc))
            .ToListAsync(ct);
}
