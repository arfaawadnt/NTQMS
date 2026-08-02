using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Authorization;

/// <summary>
/// The cross-aggregate invariant of the privilege module: after any change to
/// roles or assignments, at least one active user must still hold an active role
/// that grants <c>roles.manage</c> — otherwise the tenant has locked every
/// administrator out of the privilege screen, and only a support intervention
/// could undo it. Checked here, where roles are saved, because no single
/// aggregate can see the whole picture.
/// </summary>
internal static class ManageRolesLockoutGuard
{
    /// <summary>
    /// Throws ROLE-006 unless, under the proposed change, some active user still
    /// holds an active role granting <c>roles.manage</c>.
    /// </summary>
    /// <param name="db">Context (tenant-filtered).</param>
    /// <param name="rolesLosingManage">Roles that will no longer grant it (edited, deactivated).</param>
    /// <param name="userMovingAway">A user being moved off their current role, if any.</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task EnsureSurvivesAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> rolesLosingManage, Guid? userMovingAway, CancellationToken ct)
    {
        var stillGrantingIds = await db.Roles
            .Where(r => r.IsActive
                && !rolesLosingManage.Contains(r.Id)
                && r.Permissions.Any(p => p.PermissionKey == PermissionCatalog.ManageRoles))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var survivorExists = stillGrantingIds.Count > 0
            && await db.Users.AnyAsync(
                u => u.IsActive
                    && u.Id != userMovingAway
                    && u.RoleId != null
                    && stillGrantingIds.Contains(u.RoleId.Value),
                ct);

        if (!survivorExists)
        {
            throw new DomainException("ROLE-006",
                "This change would leave no active user able to manage roles and privileges. "
                + "Grant 'Roles & Privileges - Manage' to another active user's role first.");
        }
    }
}

file static class RoleLoader
{
    /// <summary>Loads a role in the current tenant (the global filter scopes it), or throws.</summary>
    public static async Task<Role> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("ROLE-404", "Role not found.");
}

// ── Create ────────────────────────────────────────────────────────────────────

/// <summary>Creates a tenant-defined role with an initial set of grants.</summary>
[RequirePermissionPolicy(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
public sealed record CreateRoleCommand(
    string Name, string? Description, IReadOnlyList<string> PermissionKeys, string? DefaultLanguage)
    : ICommand<Guid>;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DefaultLanguage).MaximumLength(10);
        RuleFor(x => x.PermissionKeys).NotNull();
    }
}

public sealed class CreateRoleHandler(IAppDbContext db) : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Guid> Handle(CreateRoleCommand c, CancellationToken ct)
    {
        var normalized = c.Name.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.NormalizedName == normalized, ct))
        {
            throw new DomainException("ROLE-007", $"A role named '{c.Name.Trim()}' already exists.");
        }

        var role = Role.Create(c.Name, c.Description, c.PermissionKeys, c.DefaultLanguage);
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return role.Id;
    }
}

// ── Rename / describe / language ──────────────────────────────────────────────

/// <summary>Renames a tenant-defined role and updates its description/language.</summary>
[RequirePermissionPolicy(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
public sealed record UpdateRoleCommand(
    Guid RoleId, string Name, string? Description, string? DefaultLanguage) : ICommand;

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DefaultLanguage).MaximumLength(10);
    }
}

public sealed class UpdateRoleHandler(IAppDbContext db) : ICommandHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand c, CancellationToken ct)
    {
        var role = await RoleLoader.LoadAsync(db, c.RoleId, ct);

        var normalized = c.Name.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.Id != c.RoleId && r.NormalizedName == normalized, ct))
        {
            throw new DomainException("ROLE-007", $"A role named '{c.Name.Trim()}' already exists.");
        }

        if (!role.IsSystem)
        {
            role.Rename(c.Name, c.Description);
        }

        role.SetDefaultLanguage(c.DefaultLanguage);
        await db.SaveChangesAsync(ct);
    }
}

// ── Grants ────────────────────────────────────────────────────────────────────

/// <summary>Replaces a role's grants with exactly this set, with a recorded reason.</summary>
[RequirePermissionPolicy(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
public sealed record SetRolePermissionsCommand(
    Guid RoleId, IReadOnlyList<string> PermissionKeys, string Reason) : ICommand;

public sealed class SetRolePermissionsValidator : AbstractValidator<SetRolePermissionsCommand>
{
    public SetRolePermissionsValidator()
    {
        RuleFor(x => x.PermissionKeys).NotNull();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class SetRolePermissionsHandler(IAppDbContext db) : ICommandHandler<SetRolePermissionsCommand>
{
    public async Task Handle(SetRolePermissionsCommand c, CancellationToken ct)
    {
        var role = await RoleLoader.LoadAsync(db, c.RoleId, ct);

        var losesManage = role.Grants(PermissionCatalog.ManageRoles)
            && !c.PermissionKeys.Any(k =>
                string.Equals(k?.Trim(), PermissionCatalog.ManageRoles, StringComparison.OrdinalIgnoreCase));
        if (losesManage)
        {
            await ManageRolesLockoutGuard.EnsureSurvivesAsync(db, [role.Id], userMovingAway: null, ct);
        }

        role.SetPermissions(c.PermissionKeys, c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

// ── Activate / deactivate ─────────────────────────────────────────────────────

/// <summary>Withdraws a role from assignment, or returns it.</summary>
[RequirePermissionPolicy(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
public sealed record SetRoleActiveCommand(Guid RoleId, bool Active) : ICommand;

public sealed class SetRoleActiveHandler(IAppDbContext db) : ICommandHandler<SetRoleActiveCommand>
{
    public async Task Handle(SetRoleActiveCommand c, CancellationToken ct)
    {
        var role = await RoleLoader.LoadAsync(db, c.RoleId, ct);

        if (c.Active)
        {
            role.Reactivate();
        }
        else
        {
            // Deactivating a role revokes it from every holder on their next
            // request — so it must not silence the last roles.manage holder.
            if (role.Grants(PermissionCatalog.ManageRoles))
            {
                await ManageRolesLockoutGuard.EnsureSurvivesAsync(db, [role.Id], userMovingAway: null, ct);
            }

            role.Deactivate();
        }

        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ───────────────────────────────────────────────────────────────────

/// <summary>The permission catalogue for the matrix UI.</summary>
public sealed record GetPermissionCatalogQuery : IQuery<PermissionCatalogDto>;

public sealed class GetPermissionCatalogHandler
    : IQueryHandler<GetPermissionCatalogQuery, PermissionCatalogDto>
{
    private static readonly PermissionCatalogDto Catalog = new(
        PermissionCatalog.Modules
            .Select(m => new PermissionModuleDto(
                m.Key, m.Group, m.NameKey,
                m.Actions.Select(a => a.ToString().ToLowerInvariant()).ToArray()))
            .ToArray(),
        Enum.GetValues<PermissionAction>().Select(a => a.ToString().ToLowerInvariant()).ToArray());

    public Task<PermissionCatalogDto> Handle(GetPermissionCatalogQuery q, CancellationToken ct) =>
        Task.FromResult(Catalog);
}

/// <summary>All roles of the tenant, for the privileges screen.</summary>
public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleSummaryDto>>;

public sealed class GetRolesHandler(IAppDbContext db)
    : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleSummaryDto>>
{
    public async Task<IReadOnlyList<RoleSummaryDto>> Handle(GetRolesQuery q, CancellationToken ct)
    {
        // Bound the member count to this tenant's roles. user_account has no RLS
        // (accepted deviation), so an unqualified scan here reads every tenant's
        // users into memory - harmless in the response only because role ids are
        // unique, which is one refactor away from not being true.
        var roleIds = await db.Roles.Select(r => r.Id).ToListAsync(ct);
        var members = await db.Users
            .Where(u => u.RoleId != null && roleIds.Contains(u.RoleId!.Value))
            .GroupBy(u => u.RoleId!.Value)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        var roles = await db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id, r.Name, r.Description, r.IsSystem, r.IsActive, r.DefaultLanguage,
                PermissionCount = r.Permissions.Count,
            })
            .ToListAsync(ct);

        return roles
            .Select(r => new RoleSummaryDto(
                r.Id, r.Name, r.Description, r.IsSystem, r.IsActive, r.DefaultLanguage,
                r.PermissionCount, members.GetValueOrDefault(r.Id)))
            .ToArray();
    }
}

/// <summary>One role with its grants, for the editor.</summary>
public sealed record GetRoleQuery(Guid RoleId) : IQuery<RoleDetailDto>;

public sealed class GetRoleHandler(IAppDbContext db) : IQueryHandler<GetRoleQuery, RoleDetailDto>
{
    public async Task<RoleDetailDto> Handle(GetRoleQuery q, CancellationToken ct)
    {
        var role = await db.Roles.AsNoTracking()
            .Where(r => r.Id == q.RoleId)
            .Select(r => new
            {
                r.Id, r.Name, r.Description, r.IsSystem, r.IsActive, r.DefaultLanguage,
                Keys = r.Permissions.Select(p => p.PermissionKey).ToList(),
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new DomainException("ROLE-404", "Role not found.");

        var memberCount = await db.Users.CountAsync(u => u.RoleId == q.RoleId, ct);

        return new RoleDetailDto(
            role.Id, role.Name, role.Description, role.IsSystem, role.IsActive,
            role.DefaultLanguage, role.Keys, memberCount);
    }
}

/// <summary>
/// The signed-in actor's own effective privileges — already resolved onto the
/// request by the session middleware; this query just shapes them for the SPA.
/// </summary>
public sealed record GetMyPrivilegesQuery : IQuery<MyPrivilegesDto>;

public sealed class GetMyPrivilegesHandler(IUserPrivileges privileges, IAppDbContext db, ICurrentUser user)
    : IQueryHandler<GetMyPrivilegesQuery, MyPrivilegesDto>
{
    public async Task<MyPrivilegesDto> Handle(GetMyPrivilegesQuery q, CancellationToken ct)
    {
        // The fact that a PIN exists, never its hash: the UI needs it to steer a
        // user to configure signing before their first attempt fails.
        var pinConfigured = user.UserId is { } id
            && await db.Users.AsNoTracking().Where(u => u.Id == id)
                .Select(u => u.PinHash != null).SingleOrDefaultAsync(ct);

        return new MyPrivilegesDto(
            privileges.RoleId,
            privileges.RoleName,
            privileges.IsPlatformAdmin,
            privileges.Permissions.Order(StringComparer.Ordinal).ToArray(),
            privileges.AllowedBranchIds.Order().ToArray(),
            privileges.AllowedDepartmentIds.Order().ToArray(),
            privileges.PreferredLanguage,
            pinConfigured);
    }
}
