using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
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

// ── Register ─────────────────────────────────────────────────────────────────

public sealed record RegisterUserCommand(string Email, string DisplayName, string Role, string InitialPassword)
    : ICommand<Guid>;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Role).NotEmpty();
        RuleFor(x => x.InitialPassword).NotEmpty().MinimumLength(12)
            .WithMessage("The initial password must be at least 12 characters.");
    }
}

public sealed class RegisterUserHandler(IAppDbContext db, ICurrentTenant tenant, IPasswordHasher hasher)
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

        var user = UserAccount.Create(tenantId, email, c.DisplayName, hasher.Hash(c.InitialPassword), TenantRole.Parse(c.Role));
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }
}

// ── Role / status / password ─────────────────────────────────────────────────

public sealed record ChangeUserRoleCommand(Guid UserId, string Role) : ICommand;
public sealed record SetUserActiveCommand(Guid UserId, bool Active) : ICommand;
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword) : ICommand;

public sealed class ResetUserPasswordValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordValidator() =>
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12);
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
        (await TenantUserLoader.LoadAsync(db, tenant, c.UserId, ct)).ChangeRole(TenantRole.Parse(c.Role));
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

// ── Queries ──────────────────────────────────────────────────────────────────

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
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.IsActive, u.MfaEnabled))
            .ToListAsync(ct);
    }
}

/// <summary>
/// The tenant's active-user directory for name pickers — readable by every
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
