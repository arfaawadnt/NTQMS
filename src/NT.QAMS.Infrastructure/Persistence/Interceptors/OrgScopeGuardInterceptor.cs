using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Write side of the per-user working scope. The global query filter already
/// stops a branch-restricted user from <i>loading</i> records outside their
/// branches — which blocks every edit, approval and void, since commands load
/// before they mutate. What the filter cannot stop is a <i>create</i> (or a
/// re-allocation) that points at an out-of-scope branch, so that is checked
/// here, on every added or modified <see cref="IAllocatable"/> row, in the same
/// transaction that would have persisted it.
/// <para>
/// Unrestricted actors (platform administrators, background jobs, users with no
/// scope configured) pass through untouched.
/// </para>
/// </summary>
public sealed class OrgScopeGuardInterceptor(IUserPrivileges privileges) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Guard(DbContext? context)
    {
        if (context is null || (!privileges.HasBranchRestriction && !privileges.HasDepartmentRestriction))
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<IAllocatable>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (!privileges.CanAccessBranch(entry.Entity.BranchId))
            {
                throw new DomainException(
                    "SCOPE-001",
                    "You are not permitted to work in the selected branch.");
            }

            if (!privileges.CanAccessDepartment(entry.Entity.DepartmentId))
            {
                throw new DomainException(
                    "SCOPE-002",
                    "You are not permitted to work in the selected department.");
            }
        }
    }
}
