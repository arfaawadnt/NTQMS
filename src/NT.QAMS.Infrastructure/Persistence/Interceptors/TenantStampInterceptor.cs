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
    }
}
