namespace NT.QAMS.Contracts.IdentityAccess;

// ── Permission catalogue (read-only; defined in code, rendered by the matrix UI) ──

/// <summary>One configurable module of the privilege matrix.</summary>
/// <param name="Key">Stable module key used inside permission keys.</param>
/// <param name="Group">Navigation group for rendering.</param>
/// <param name="NameKey">i18n key of the module's display name.</param>
/// <param name="Actions">Lower-case action names meaningful for this module.</param>
public sealed record PermissionModuleDto(
    string Key, string Group, string NameKey, IReadOnlyList<string> Actions);

/// <summary>The whole permission catalogue, in render order.</summary>
/// <param name="Modules">Modules grouped and ordered as the matrix shows them.</param>
/// <param name="Actions">Every action name, in column order.</param>
public sealed record PermissionCatalogDto(
    IReadOnlyList<PermissionModuleDto> Modules, IReadOnlyList<string> Actions);

// ── Roles ─────────────────────────────────────────────────────────────────────

/// <summary>A role as listed in the privileges screen.</summary>
public sealed record RoleSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    string? DefaultLanguage,
    int PermissionCount,
    int MemberCount);

/// <summary>A role opened for editing: the summary plus its granted keys.</summary>
public sealed record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    string? DefaultLanguage,
    IReadOnlyList<string> PermissionKeys,
    int MemberCount);

public sealed record CreateRoleRequest(
    string Name, string? Description, IReadOnlyList<string> PermissionKeys, string? DefaultLanguage);

public sealed record UpdateRoleRequest(string Name, string? Description, string? DefaultLanguage);

/// <summary>Replaces a role's grants. The reason lands in the audit trail.</summary>
public sealed record SetRolePermissionsRequest(IReadOnlyList<string> PermissionKeys, string Reason);

public sealed record SetRoleActiveRequest(bool Active);

// ── User assignment ───────────────────────────────────────────────────────────

public sealed record AssignUserRoleRequest(Guid RoleId);

/// <summary>Sets a user's working scope. Empty lists mean unrestricted.</summary>
public sealed record SetUserScopeRequest(
    IReadOnlyList<Guid> BranchIds, IReadOnlyList<Guid> DepartmentIds);

public sealed record SetUserLanguageRequest(string? Language);

// ── The signed-in actor's own privileges (SPA bootstrap) ─────────────────────

/// <summary>
/// What the signed-in user may do — the SPA reads this once after sign-in and
/// drives navigation, buttons and guards from it. The server enforces the same
/// facts independently on every request; this DTO only tells the UI what is
/// worth offering.
/// </summary>
public sealed record MyPrivilegesDto(
    Guid? RoleId,
    string? RoleName,
    bool IsPlatformAdmin,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> DepartmentIds,
    string? PreferredLanguage);
