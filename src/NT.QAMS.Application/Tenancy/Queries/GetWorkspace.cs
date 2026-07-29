using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Tenancy;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Tenancy.Queries;

/// <summary>
/// Resolves a laboratory's display name from the slug in its own sign-in address
/// (/t/{slug}) so the login page can greet the lab by NAME rather than echoing
/// the identifier back at the user.
/// <para>
/// Deliberately minimal and anonymous: it is consumed before authentication, so
/// it returns the name and nothing else — no ids, no status, no settings. An
/// unknown slug, a malformed slug, and a non-active tenant are all reported the
/// same way (no result), so the endpoint cannot be used to probe tenant state.
/// </para>
/// </summary>
public sealed record GetWorkspaceQuery(string? Slug) : IQuery<WorkspaceResponse?>;

internal sealed class GetWorkspaceQueryHandler(IAppDbContext db)
    : IQueryHandler<GetWorkspaceQuery, WorkspaceResponse?>
{
    public async Task<WorkspaceResponse?> Handle(GetWorkspaceQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Slug))
        {
            return null;
        }

        TenantSlug slug;
        try
        {
            slug = TenantSlug.Create(query.Slug);
        }
        catch (DomainException)
        {
            // A malformed slug is simply not a workspace — same answer as unknown.
            return null;
        }

        return await db.Tenants.AsNoTracking()
            .Where(t => t.Slug == slug && t.Status == TenantStatus.Active)
            .Select(t => new WorkspaceResponse(t.Name))
            .SingleOrDefaultAsync(ct);
    }
}
