using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.Infrastructure.Authorization;

/// <summary>
/// Scoped holder for the current request's privileges. Written once by the
/// privilege middleware, read by controllers, behaviours and query scoping.
/// Unresolved by default, so a code path that forgets to authorize denies rather
/// than permits.
/// </summary>
public sealed class RequestPrivileges : IUserPrivileges, IUserPrivilegesSetter
{
    private static readonly IReadOnlySet<string> NoPermissions = new HashSet<string>(StringComparer.Ordinal);
    private static readonly IReadOnlySet<Guid> NoScope = new HashSet<Guid>();

    private ResolvedPrivileges? _resolved;

    public bool IsResolved => _resolved is not null || IsPlatformAdmin;

    public bool IsPlatformAdmin { get; private set; }

    public Guid? RoleId => _resolved?.RoleId;

    public string? RoleName => _resolved?.RoleName;

    public IReadOnlySet<string> Permissions => _resolved?.Permissions ?? NoPermissions;

    public IReadOnlySet<Guid> AllowedBranchIds => _resolved?.AllowedBranchIds ?? NoScope;

    public IReadOnlySet<Guid> AllowedDepartmentIds => _resolved?.AllowedDepartmentIds ?? NoScope;

    public bool HasBranchRestriction => !IsPlatformAdmin && AllowedBranchIds.Count > 0;

    public bool HasDepartmentRestriction => !IsPlatformAdmin && AllowedDepartmentIds.Count > 0;

    public string? PreferredLanguage => _resolved?.PreferredLanguage;

    public bool Has(string permissionKey) => IsPlatformAdmin || Permissions.Contains(permissionKey);

    public bool CanAccessBranch(Guid? branchId) =>
        !HasBranchRestriction || branchId is null || AllowedBranchIds.Contains(branchId.Value);

    public bool CanAccessDepartment(Guid? departmentId) =>
        !HasDepartmentRestriction || departmentId is null || AllowedDepartmentIds.Contains(departmentId.Value);

    public void Set(ResolvedPrivileges privileges) => _resolved = privileges;

    public void SetPlatformAdmin() => IsPlatformAdmin = true;
}

/// <summary>
/// Reads privileges straight from the database on each authenticated request.
/// <para>
/// Deliberately uncached. A cache here would need invalidating from role edits,
/// user edits, branch moves and department moves, and the failure mode of a missed
/// invalidation is a user retaining a revoked privilege — the one outcome an access
/// control must not have. The cost is two small indexed reads, on a request that
/// already loads the user row for session revocation.
/// </para>
/// </summary>
public sealed class PrivilegeResolver(IAppDbContext db) : IPrivilegeResolver
{
    public async Task<ResolvedPrivileges?> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.RoleId,
                u.PreferredLanguage,
                BranchIds = u.BranchAccess.Select(b => b.BranchId).ToList(),
                DepartmentIds = u.DepartmentAccess.Select(d => d.DepartmentId).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        string? roleName = null;
        string? roleLanguage = null;
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        if (user.RoleId is { } roleId)
        {
            // An inactive role grants nothing: deactivating a role must actually
            // stop its holders, not merely stop new assignments.
            var role = await db.Roles.AsNoTracking()
                .Where(r => r.Id == roleId && r.IsActive)
                .Select(r => new
                {
                    r.Name,
                    r.DefaultLanguage,
                    Keys = r.Permissions.Select(p => p.PermissionKey).ToList(),
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (role is not null)
            {
                roleName = role.Name;
                roleLanguage = role.DefaultLanguage;
                permissions.UnionWith(role.Keys);
            }
        }

        return new ResolvedPrivileges(
            user.RoleId,
            roleName,
            permissions,
            user.BranchIds.ToHashSet(),
            user.DepartmentIds.ToHashSet(),
            user.PreferredLanguage ?? roleLanguage);
    }
}
