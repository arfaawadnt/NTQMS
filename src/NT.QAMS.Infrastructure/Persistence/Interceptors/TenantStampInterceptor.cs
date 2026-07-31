using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps TenantId on every added ITenantScoped entity from the resolved
/// request tenant. Throws — never guesses — when the tenant is unresolved:
/// silently unscoped rows are the one unforgivable multi-tenancy defect.
/// </summary>
public sealed class TenantStampInterceptor(ICurrentTenant currentTenant)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (entry.Entity.TenantId != Guid.Empty)
            {
                continue; // Explicitly scoped (e.g. provisioning saga seeding a new tenant).
            }

            entry.Entity.TenantId = currentTenant.TenantId
                ?? throw new DomainException(
                    "TENANT-000",
                    $"Cannot persist tenant-scoped '{entry.Metadata.ClrType.Name}' without a resolved tenant.");
        }

        StampOwnedChildren(context);
    }

    /// <summary>
    /// Schema hardening Phase 4: owned child tables carry a shadow
    /// <c>tenant_id</c> so RLS can fence them directly (the CASCADE FK never
    /// isolated reads). The value is copied from the tracked owner — which
    /// also serves elevated seeding, where no request tenant exists — falling
    /// back to the request tenant. The composite FK to the owner makes a
    /// mismatched value impossible to persist regardless of what happens here.
    /// </summary>
    private void StampOwnedChildren(DbContext context)
    {
        Dictionary<Guid, Guid>? ownerTenants = null;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added || !entry.Metadata.IsOwned())
            {
                continue;
            }

            var tenantProperty = entry.Metadata.FindProperty("TenantId");
            if (tenantProperty is null || !tenantProperty.IsShadowProperty())
            {
                continue;
            }

            var current = entry.Property("TenantId").CurrentValue;
            if (current is Guid set && set != Guid.Empty)
            {
                continue;
            }

            ownerTenants ??= CollectOwnerTenants(context);

            var ownership = entry.Metadata.FindOwnership()!;
            var ownerId = ownership.Properties
                .Select(p => entry.Property(p.Name).CurrentValue)
                .OfType<Guid>()
                .FirstOrDefault();

            if (ownerTenants.TryGetValue(ownerId, out var ownerTenant))
            {
                entry.Property("TenantId").CurrentValue = ownerTenant;
            }
            else
            {
                entry.Property("TenantId").CurrentValue = currentTenant.TenantId
                    ?? throw new DomainException(
                        "TENANT-000",
                        $"Cannot persist owned '{entry.Metadata.ClrType.Name}' without a resolved tenant.");
            }
        }
    }

    /// <summary>Tenants of every tracked aggregate that could own a child, keyed by id.</summary>
    private static Dictionary<Guid, Guid> CollectOwnerTenants(DbContext context)
    {
        var map = new Dictionary<Guid, Guid>();
        foreach (var entry in context.ChangeTracker.Entries<SharedKernel.Primitives.AggregateRoot>())
        {
            switch (entry.Entity)
            {
                case ITenantScoped { TenantId: var t } when t != Guid.Empty:
                    map[entry.Entity.Id] = t;
                    break;
                case IOptionallyTenantScoped { TenantId: { } ot }:
                    map[entry.Entity.Id] = ot;
                    break;
            }
        }

        return map;
    }
}
