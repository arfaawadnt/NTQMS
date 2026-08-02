using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IdentityAccess.Commands;

/// <summary>Parses a role name to the enum, rejecting unknown or platform-admin values for tenant users.</summary>
internal static class TenantRole
{
    public static UserRole Parse(string role)
    {
        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed))
        {
            throw new DomainException("USER-007", $"Unknown role '{role}'.");
        }

        if (parsed == UserRole.PlatformAdmin)
        {
            throw new DomainException("USER-005", "Platform administrator is not a tenant role.");
        }

        return parsed;
    }
}

// â”€â”€ Register â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record RegisterUserCommand(
    string Email, string DisplayName, string Role, string InitialPassword, Guid? RoleId = null,
    string? InitialPin = null)
    : ICommand<Guid>;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Role).NotEmpty();
        RuleFor(x => x.InitialPassword).StrongPassword();
        RuleFor(x => x.InitialPin).Matches("^[0-9]{4}$")
            .When(x => !string.IsNullOrEmpty(x.InitialPin))
            .WithMessage("The e-signature PIN must be exactly 4 digits.");
    }
}

public sealed class RegisterUserHandler(IAppDbContext db, ICurrentTenant tenant, IPasswordHasher hasher, ISecurityEventLog security)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        var email = c.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == email, ct))
        {
            throw new DomainException("USER-008", $"A user with email '{email}' already exists in this tenant.");
        }

        var tier = TenantRole.Parse(c.Role);
        var user = UserAccount.Create(tenantId, email, c.DisplayName, hasher.Hash(c.InitialPassword), tier);

        if (c.RoleId is { } roleId)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, ct)
                ?? throw new DomainException("ROLE-404", "Role not found.");
            if (!role.IsActive)
            {
                throw new DomainException("ROLE-008", "An inactive role cannot be assigned.");
            }

            user.AssignRole(role.Id);
        }
        else
        {
            // Callers that still speak the tier-based contract get the seeded
            // role that reproduces the tier - an account must never be born
            // without privileges just because the caller predates the module.
            await Authorization.SeededRoleDefault.AssignAsync(db, user, tier, ct);
        }

        if (!string.IsNullOrEmpty(c.InitialPin))
        {
            // Admin-issued, so ledgered under its own event type: an auditor must
            // always be able to tell an issued signing credential from a self-set
            // one, because until the user rotates it two people know it.
            user.SetPin(hasher.Hash(c.InitialPin));
            await security.WriteAsync("PIN_ADMIN_SET", tenantId, email, "at-registration", ct);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }
}

// â”€â”€ Role / status / password â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record ChangeUserRoleCommand(Guid UserId, string Role) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record SetUserActiveCommand(Guid UserId, bool Active) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword) : ICommand;

/// <summary>
/// Admin set/reset of a user's e-signature PIN — the recovery path when a user
/// cannot sign. Ledgered as PIN_ADMIN_SET, distinct from self-service PIN_SET,
/// so issued credentials are always distinguishable in the trail; the holder
/// should rotate it from their account menu.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record SetUserPinCommand(Guid UserId, string Pin) : ICommand;

public sealed class SetUserPinValidator : AbstractValidator<SetUserPinCommand>
{
    public SetUserPinValidator() =>
        RuleFor(x => x.Pin).NotEmpty().Matches("^[0-9]{4}$")
            .WithMessage("The e-signature PIN must be exactly 4 digits.");
}

public sealed class SetUserPinHandler(
    IAppDbContext db, ICurrentTenant tenant, IPasswordHasher hasher, ISecurityEventLog security)
    : ICommandHandler<SetUserPinCommand>
{
    public async Task Handle(SetUserPinCommand c, CancellationToken ct)
    {
        var user = await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct);
        user.SetPin(hasher.Hash(c.Pin));
        await security.WriteAsync("PIN_ADMIN_SET", tenant.TenantId, user.Email, "by-administrator", ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ResetUserPasswordValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordValidator() =>
        RuleFor(x => x.NewPassword).StrongPassword();
}

internal static class TenantUserLoader
{
    /// <summary>Loads a user that belongs to the current tenant, or throws.</summary>
    public static async Task<UserAccount> LoadAsync(IAppDbContext db, ICurrentTenant tenant, Guid id, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        return await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct)
            ?? throw new DomainException("USER-404", "User not found.");
    }
}

public sealed class ChangeUserRoleHandler(IAppDbContext db, ICurrentTenant tenant)
    : ICommandHandler<ChangeUserRoleCommand>
{
    public async Task Handle(ChangeUserRoleCommand c, CancellationToken ct)
    {
        var user = await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct);
        var tier = TenantRole.Parse(c.Role);
        user.ChangeRole(tier);
        // The tier-based contract changes privileges too: follow to the seeded
        // role that reproduces the new tier, exactly as registration does.
        await Authorization.SeededRoleDefault.AssignAsync(db, user, tier, ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SetUserActiveHandler(IAppDbContext db, ICurrentTenant tenant)
    : ICommandHandler<SetUserActiveCommand>
{
    public async Task Handle(SetUserActiveCommand c, CancellationToken ct)
    {
        var user = await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct);
        if (c.Active) { user.Reactivate(); } else { user.Deactivate(); }
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ResetUserPasswordHandler(IAppDbContext db, ICurrentTenant tenant, IPasswordHasher hasher)
    : ICommandHandler<ResetUserPasswordCommand>
{
    public async Task Handle(ResetUserPasswordCommand c, CancellationToken ct)
    {
        (await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct)).ResetPassword(hasher.Hash(c.NewPassword));
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetUsersQuery : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersHandler(IAppDbContext db, ICurrentTenant tenant)
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery q, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        return await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserDto(
                u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.IsActive, u.MfaEnabled,
                u.RoleId,
                db.Roles.Where(r => r.Id == u.RoleId).Select(r => r.Name).FirstOrDefault(),
                u.BranchAccess.Select(b => b.BranchId).ToList(),
                u.DepartmentAccess.Select(d => d.DepartmentId).ToList(),
                u.PreferredLanguage,
                u.PinHash != null))
            .ToListAsync(ct);
    }
}

/// <summary>
/// The tenant's active-user directory for name pickers â€” readable by every
/// authenticated tenant user (unlike full user administration). Exposes ids,
/// display names and roles only.
/// </summary>
public sealed record GetUserDirectoryQuery : IQuery<IReadOnlyList<UserDirectoryEntryDto>>;

public sealed class GetUserDirectoryHandler(IAppDbContext db, ICurrentTenant tenant)
    : IQueryHandler<GetUserDirectoryQuery, IReadOnlyList<UserDirectoryEntryDto>>
{
    public async Task<IReadOnlyList<UserDirectoryEntryDto>> Handle(GetUserDirectoryQuery q, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        return await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserDirectoryEntryDto(u.Id, u.DisplayName, u.Role.ToString()))
            .ToListAsync(ct);
    }
}

// ── Configurable role, working scope, language ─────────────────────────────────

/// <summary>Moves a user onto a different configurable role.</summary>
[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record AssignUserRoleCommand(Guid UserId, Guid RoleId) : ICommand;

public sealed class AssignUserRoleHandler(IAppDbContext db, ICurrentTenant tenant)
    : ICommandHandler<AssignUserRoleCommand>
{
    public async Task Handle(AssignUserRoleCommand c, CancellationToken ct)
    {
        var user = await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct);

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == c.RoleId, ct)
            ?? throw new DomainException("ROLE-404", "Role not found.");
        if (!role.IsActive)
        {
            throw new DomainException("ROLE-008", "An inactive role cannot be assigned.");
        }

        // Moving this user off a roles.manage-granting role must not leave the
        // tenant without an administrator of privileges.
        if (user.RoleId is { } current && current != role.Id && !role.Grants(PermissionCatalog.ManageRoles))
        {
            var leavingManage = await db.Roles
                .AnyAsync(r => r.Id == current
                    && r.Permissions.Any(p => p.PermissionKey == PermissionCatalog.ManageRoles), ct);
            if (leavingManage)
            {
                await Authorization.ManageRolesLockoutGuard.EnsureSurvivesAsync(db, [], user.Id, ct);
            }
        }

        user.AssignRole(role.Id);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Sets the branches and departments a user may work in. Empty lists mean
/// unrestricted — the explicit widest case, which the domain records as its own
/// auditable fact.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record SetUserScopeCommand(
    Guid UserId, IReadOnlyList<Guid> BranchIds, IReadOnlyList<Guid> DepartmentIds) : ICommand;

public sealed class SetUserScopeValidator : AbstractValidator<SetUserScopeCommand>
{
    public SetUserScopeValidator()
    {
        RuleFor(x => x.BranchIds).NotNull();
        RuleFor(x => x.DepartmentIds).NotNull();
    }
}

public sealed class SetUserScopeHandler(IAppDbContext db, ICurrentTenant tenant)
    : ICommandHandler<SetUserScopeCommand>
{
    public async Task Handle(SetUserScopeCommand c, CancellationToken ct)
    {
        var user = await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct);

        // The scope must point at the tenant's own org units — a foreign or
        // deleted id would silently restrict the user to nothing.
        var branchIds = c.BranchIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (branchIds.Count > 0)
        {
            var known = await db.Branches.CountAsync(b => branchIds.Contains(b.Id), ct);
            if (known != branchIds.Count)
            {
                throw new DomainException("SCOPE-003", "One or more selected branches do not exist.");
            }
        }

        var departmentIds = c.DepartmentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (departmentIds.Count > 0)
        {
            var known = await db.Departments.CountAsync(d => departmentIds.Contains(d.Id), ct);
            if (known != departmentIds.Count)
            {
                throw new DomainException("SCOPE-004", "One or more selected departments do not exist.");
            }
        }

        user.SetScope(branchIds, departmentIds);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Sets a user's interface language (administration side).</summary>
[RequirePermissionPolicy(PermissionCatalog.Users, PermissionAction.Manage)]
public sealed record SetUserLanguageCommand(Guid UserId, string? Language) : ICommand;

public sealed class SetUserLanguageValidator : AbstractValidator<SetUserLanguageCommand>
{
    public SetUserLanguageValidator() => RuleFor(x => x.Language).MaximumLength(10);
}

public sealed class SetUserLanguageHandler(IAppDbContext db, ICurrentTenant tenant)
    : ICommandHandler<SetUserLanguageCommand>
{
    public async Task Handle(SetUserLanguageCommand c, CancellationToken ct)
    {
        (await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct)).SetPreferredLanguage(c.Language);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// The signed-in user's own language choice — self-service, so it needs no
/// administrative privilege, and it wins over the role and tenant defaults.
/// </summary>
[RequireAuthenticatedActor]
public sealed record SetMyLanguageCommand(string? Language) : ICommand;

public sealed class SetMyLanguageValidator : AbstractValidator<SetMyLanguageCommand>
{
    public SetMyLanguageValidator() => RuleFor(x => x.Language).MaximumLength(10);
}

public sealed class SetMyLanguageHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<SetMyLanguageCommand>
{
    public async Task Handle(SetMyLanguageCommand c, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new DomainException("AUTHZ-001", "An authenticated actor is required for this action.");
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("USER-404", "User not found.");

        user.SetPreferredLanguage(c.Language);
        await db.SaveChangesAsync(ct);
    }
}
