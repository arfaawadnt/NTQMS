using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.QualityIndicators;
using NT.QAMS.Domain.QualityIndicators;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.QualityIndicators.Queries;

public sealed record GetIndicatorsQuery(
    string? Status = null, string? Search = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<IndicatorListItemDto>>;

public sealed class GetIndicatorsHandler(IAppDbContext db)
    : IQueryHandler<GetIndicatorsQuery, Contracts.Common.PagedResponse<IndicatorListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<IndicatorListItemDto>> Handle(
        GetIndicatorsQuery q, CancellationToken ct)
    {
        var query = db.QualityIndicators.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(i => i.Status.ToString() == q.Status);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(i => i.Name.Contains(term) || i.Code.Contains(term) || i.IndicatorRef.Contains(term));
        }

        return await query
            .OrderBy(i => i.Code)
            .Select(i => new IndicatorListItemDto(
                i.Id, i.IndicatorRef, i.Code, i.Name, i.Unit, i.Frequency.ToString(),
                i.Direction.ToString(), i.Status.ToString(), i.Target,
                i.Measurements.OrderByDescending(m => m.Period).Select(m => (decimal?)m.Value).FirstOrDefault(),
                i.Measurements.OrderByDescending(m => m.Period).Select(m => m.Status.ToString()).FirstOrDefault(),
                i.Measurements.OrderByDescending(m => m.Period).Select(m => (DateOnly?)m.Period).FirstOrDefault()))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetIndicatorByIdQuery(Guid IndicatorId) : IQuery<IndicatorDetailDto>;

public sealed class GetIndicatorByIdHandler(IAppDbContext db) : IQueryHandler<GetIndicatorByIdQuery, IndicatorDetailDto>
{
    public async Task<IndicatorDetailDto> Handle(GetIndicatorByIdQuery q, CancellationToken ct)
    {
        var i = await db.QualityIndicators
            .AsNoTracking()
            .Include(x => x.Measurements)
            .SingleOrDefaultAsync(x => x.Id == q.IndicatorId, ct)
            ?? throw new DomainException("IND-404", "Indicator not found.");

        return new IndicatorDetailDto(
            i.Id, i.IndicatorRef, i.Code, i.Name, i.Description,
            i.Numerator, i.Denominator, i.Inclusions, i.Exclusions, i.DataSource,
            i.Unit, i.RateFactor, i.Frequency.ToString(), i.Direction.ToString(), i.Status.ToString(),
            i.Target, i.WarningThreshold, i.ActionThreshold,
            i.Measurements
                .OrderBy(m => m.Period)
                .Select(m => new IndicatorMeasurementDto(
                    m.Id, m.Period, m.Numerator, m.Denominator, m.Value, m.Status.ToString(),
                    m.EnteredBy, m.RecordedAtUtc, m.Note)).ToList());
    }
}

/// <summary>
/// Runs statistical process control over the indicator's measurement series (chronological)
/// and returns the control limits plus each point graded for special-cause variation, so the
/// UI can draw a control chart that separates real signal from ordinary noise.
/// </summary>
public sealed record GetIndicatorControlChartQuery(Guid IndicatorId) : IQuery<IndicatorControlChartDto>;

public sealed class GetIndicatorControlChartHandler(IAppDbContext db)
    : IQueryHandler<GetIndicatorControlChartQuery, IndicatorControlChartDto>
{
    public async Task<IndicatorControlChartDto> Handle(GetIndicatorControlChartQuery q, CancellationToken ct)
    {
        var i = await db.QualityIndicators
            .AsNoTracking()
            .Include(x => x.Measurements)
            .SingleOrDefaultAsync(x => x.Id == q.IndicatorId, ct)
            ?? throw new DomainException("IND-404", "Indicator not found.");

        var ordered = i.Measurements.OrderBy(m => m.Period).ToList();
        var analysis = IndicatorSpc.Analyze(ordered.Select(m => m.Value).ToList());

        var points = ordered
            .Select((m, idx) =>
            {
                var p = analysis.HasLimits ? analysis.Points[idx] : null;
                return new SpcPointDto(m.Period, m.Value, p?.SpecialCause ?? false, p?.Rules ?? []);
            })
            .ToList();

        return new IndicatorControlChartDto(
            i.Id, i.Code, i.Unit, analysis.HasLimits,
            analysis.Mean, analysis.StdDev, analysis.Ucl, analysis.Lcl,
            analysis.Upper2Sigma, analysis.Lower2Sigma, analysis.Upper1Sigma, analysis.Lower1Sigma,
            i.Target, i.WarningThreshold, i.ActionThreshold, points);
    }
}
