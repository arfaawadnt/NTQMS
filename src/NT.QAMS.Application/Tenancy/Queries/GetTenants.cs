using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Tenancy;

namespace NT.QAMS.Application.Tenancy.Queries;

/// <summary>Control-plane query: list tenants. Read side — no aggregates, no tracking.</summary>
public sealed record GetTenantsQuery : IQuery<IReadOnlyList<TenantDto>>;

public sealed class GetTenantsHandler(IAppDbContext db)
    : IQueryHandler<GetTenantsQuery, IReadOnlyList<TenantDto>>
{
    public async Task<IReadOnlyList<TenantDto>> Handle(
        GetTenantsQuery query, CancellationToken cancellationToken)
    {
        return await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(
                t.Id,
                t.Slug.Value,
                t.Name,
                t.Status.ToString(),
                t.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
