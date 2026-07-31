namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// The authenticated actor's effective privileges for the current request: what
/// they may do, and which branches and departments they may do it in.
/// <para>
/// Resolved from the database on each authenticated request rather than carried in
/// the token. That costs one query, and buys the property the regulation actually
/// needs: when an administrator revokes a privilege it takes effect on the user's
/// very next request, instead of lingering until their token expires.
/// </para>
/// <para>
/// Both scope sets follow the same rule — <b>empty means unrestricted</b>. A user
/// with no branch entries works across the whole tenant; adding one entry turns the
/// set into a closed list. This is what keeps the upgrade safe for accounts that
/// existed before scoping, and it is why callers must ask
/// <see cref="CanAccessBranch"/> rather than testing the set for membership.
/// </para>
/// </summary>
public interface IUserPrivileges
{
    /// <summary>False for anonymous requests and for platform-level requests with no tenant.</summary>
    bool IsResolved { get; }

    /// <summary>
    /// True when the actor is a platform administrator. They sit outside tenant
    /// data entirely, so tenant privilege checks do not apply to them.
    /// </summary>
    bool IsPlatformAdmin { get; }

    /// <summary>The tenant-defined role the actor holds, if any.</summary>
    Guid? RoleId { get; }

    /// <summary>The role's display name, for problem responses and the audit trail.</summary>
    string? RoleName { get; }

    /// <summary>Granted permission keys from <c>PermissionCatalog</c>.</summary>
    IReadOnlySet<string> Permissions { get; }

    /// <summary>Allowed branches; empty means unrestricted.</summary>
    IReadOnlySet<Guid> AllowedBranchIds { get; }

    /// <summary>Allowed departments; empty means unrestricted.</summary>
    IReadOnlySet<Guid> AllowedDepartmentIds { get; }

    /// <summary>True when a closed branch list applies to this actor.</summary>
    bool HasBranchRestriction { get; }

    /// <summary>True when a closed department list applies to this actor.</summary>
    bool HasDepartmentRestriction { get; }

    /// <summary>Effective interface language: the user's choice, else the role default, else null.</summary>
    string? PreferredLanguage { get; }

    /// <summary>True when the actor holds this permission.</summary>
    bool Has(string permissionKey);

    /// <summary>
    /// True when the actor may work in this branch. A null branch means the record
    /// is not attributed to a branch; such records stay visible, because hiding
    /// unattributed evidence from a scoped user would silently shrink the quality
    /// record rather than restrict it.
    /// </summary>
    bool CanAccessBranch(Guid? branchId);

    /// <summary>True when the actor may work in this department. Null is permitted, as for branches.</summary>
    bool CanAccessDepartment(Guid? departmentId);
}

/// <summary>Write side of <see cref="IUserPrivileges"/>, for the request pipeline only.</summary>
public interface IUserPrivilegesSetter
{
    /// <summary>Records the resolved privileges for this request.</summary>
    void Set(ResolvedPrivileges privileges);

    /// <summary>Marks the actor as a platform administrator (no tenant privileges apply).</summary>
    void SetPlatformAdmin();
}

/// <summary>The resolved privilege snapshot handed to <see cref="IUserPrivilegesSetter"/>.</summary>
/// <param name="RoleId">The role the user holds.</param>
/// <param name="RoleName">Role display name.</param>
/// <param name="Permissions">Granted permission keys.</param>
/// <param name="AllowedBranchIds">Allowed branches; empty means unrestricted.</param>
/// <param name="AllowedDepartmentIds">Allowed departments; empty means unrestricted.</param>
/// <param name="PreferredLanguage">Effective language, already resolved through user → role.</param>
public sealed record ResolvedPrivileges(
    Guid? RoleId,
    string? RoleName,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<Guid> AllowedBranchIds,
    IReadOnlySet<Guid> AllowedDepartmentIds,
    string? PreferredLanguage);

/// <summary>
/// Loads an actor's effective privileges. Implemented over the database so that a
/// privilege change is felt on the next request; called once per authenticated
/// request by the pipeline, never from handlers.
/// </summary>
public interface IPrivilegeResolver
{
    /// <summary>
    /// Resolves the user's role, permissions, working scope and language, or null
    /// when the user holds no tenant-defined role yet.
    /// </summary>
    Task<ResolvedPrivileges?> ResolveAsync(Guid userId, CancellationToken cancellationToken);
}
