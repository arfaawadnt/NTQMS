using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IdentityAccess.Commands;

/// <summary>
/// Password (+ MFA, if enrolled) login with account lockout. AUTH-coded failures
/// map to 401 and never reveal which factor failed. Every attempt â€” success,
/// bad password, bad MFA, lockout â€” is written to the security-event ledger.
/// </summary>
[AllowUnauthenticated]
public sealed record LoginCommand(string? TenantIdentifier, string Email, string Password, string? MfaCode)
    : ICommand<LoginResult>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler(
    IAppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt,
    ITotpService totp, ISecurityEventLog security, IClock clock, PasswordPolicyOptions passwordPolicy,
    SecurityOptions securityOptions, RefreshSessionOptions refreshOptions,
    ICurrentTenantSetter tenantScope)
    : ICommandHandler<LoginCommand, LoginResult>
{
    private const string InvalidCredentials = "Invalid credentials.";

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken ct)
    {
        Guid? tenantId = null;
        // MFA enforcement is decided per tenant for tenant users (their own opt-in
        // setting); platform admins fall back to the global SecurityOptions flag.
        bool requireMfaPolicy = securityOptions.RequireMfaForPrivilegedRoles;

        if (!string.IsNullOrWhiteSpace(command.TenantIdentifier))
        {
            var slug = TenantSlug.Create(command.TenantIdentifier);
            var tenant = await db.Tenants.AsNoTracking()
                .SingleOrDefaultAsync(t => t.Slug == slug, ct)
                ?? throw await FailAsync("AUTH-001", InvalidCredentials, null, command.Email, "unknown-tenant", ct);

            // The request declared its workspace; scope it now, BEFORE any path
            // that writes a tenant-stamped security event - the security_event
            // RLS WITH CHECK (Phase 2) requires the connection to carry the
            // tenant it writes for. This also makes failed logins visible to
            // the tenant's own compliance view, where they belong.
            tenantScope.Set(tenant.Id);

            if (tenant.Status != TenantStatus.Active)
            {
                throw await FailAsync("AUTH-002", "This tenant is not active.", tenant.Id, command.Email, "tenant-inactive", ct);
            }

            tenantId = tenant.Id;
            requireMfaPolicy = tenant.Settings.RequireMfaForPrivilegedRoles;
        }

        var email = command.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

        if (user is null || !user.IsActive)
        {
            throw await FailAsync("AUTH-001", InvalidCredentials, tenantId, email, "no-such-user", ct);
        }

        if (user.IsLockedOut(clock.UtcNow))
        {
            throw await FailAsync("AUTH-004", "Account is temporarily locked. Try again later.", tenantId, email, "locked-out", ct);
        }

        if (!hasher.Verify(user.PasswordHash, command.Password))
        {
            user.RegisterFailedLogin(clock.UtcNow);
            await db.SaveChangesAsync(ct);
            throw await FailAsync("AUTH-001", InvalidCredentials, tenantId, email, "bad-password", ct);
        }

        // Part 11 Â§11.300(b): force rotation once the password exceeds its maximum age.
        if (passwordPolicy.MaxAgeDays > 0
            && user.PasswordChangedAtUtc is { } changed
            && changed.AddDays(passwordPolicy.MaxAgeDays) < clock.UtcNow)
        {
            throw await FailAsync(
                "AUTH-101", "Password has expired and must be changed.", tenantId, email, "password-expired", ct);
        }

        if (user.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(command.MfaCode))
            {
                // Password OK but MFA required â€” signal the client to collect a code.
                await security.WriteAsync("LOGIN_MFA_REQUIRED", tenantId, email, null, ct);
                return new LoginResult(
                    new AuthResponse(string.Empty, default, user.Role.ToString(), user.DisplayName, user.TenantId, MfaRequired: true),
                    Refresh: null);
            }

            if (!totp.Verify(user.MfaSecret!, command.MfaCode, clock.UtcNow))
            {
                user.RegisterFailedLogin(clock.UtcNow);
                await db.SaveChangesAsync(ct);
                throw await FailAsync("AUTH-005", InvalidCredentials, tenantId, email, "bad-mfa", ct);
            }
        }

        // F-04 (Part 11 Â§11.10(d)): a privileged user who has not enrolled MFA is
        // given only an enrollment-scoped session until they set it up. Enforced
        // by MfaEnrollmentGateMiddleware; opt-in per environment (off by default).
        var mustEnrollMfa = requireMfaPolicy
            && !user.MfaEnabled
            && user.Role is UserRole.PlatformAdmin or UserRole.TenantAdmin;

        user.RegisterSuccessfulLogin();

        // ADR-0009: a FULL session starts a fresh refresh-token family; the
        // enrollment-scoped intermediate gets none (it must not outlive setup).
        RefreshGrant? refresh = null;
        if (!mustEnrollMfa)
        {
            var (refreshToken, hash, sessionId) = RefreshTokenFormat.Mint();
            var refreshSession = RefreshSession.Start(
                sessionId, user.Id, Guid.CreateVersion7(), hash, clock.UtcNow, refreshOptions.Lifetime);
            db.RefreshSessions.Add(refreshSession);
            refresh = new RefreshGrant(refreshToken, refreshSession.ExpiresAtUtc);
        }

        await db.SaveChangesAsync(ct);
        await security.WriteAsync(mustEnrollMfa ? "LOGIN_MFA_ENROLL_REQUIRED" : "LOGIN_SUCCESS", tenantId, email, null, ct);

        var (token, expires) = jwt.Issue(user, enrollmentOnly: mustEnrollMfa);
        return new LoginResult(
            new AuthResponse(
                token, expires, user.Role.ToString(), user.DisplayName, user.TenantId,
                MfaRequired: false, MfaEnrollmentRequired: mustEnrollMfa),
            refresh);
    }

    private async Task<DomainException> FailAsync(
        string code, string message, Guid? tenantId, string email, string reason, CancellationToken ct)
    {
        await security.WriteAsync("LOGIN_FAILED", tenantId, email, reason, ct);
        return new DomainException(code, message);
    }
}


// â”€â”€ Self-service password change (works while the password is expired) â”€â”€â”€â”€â”€â”€

[AllowUnauthenticated]
public sealed record ChangePasswordCommand(
    string? TenantIdentifier, string Email, string CurrentPassword, string NewPassword) : ICommand;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}

/// <summary>
/// Rotates a password after verifying the full current credentials (usable
/// pre-login, so an expired password can be changed). Enforces the reuse ban
/// against the current hash plus the configured history depth, retires the
/// old hash into the history, and logs a PASSWORD_CHANGED security event.
/// </summary>
public sealed class ChangePasswordHandler(
    IAppDbContext db, IPasswordHasher hasher, ISecurityEventLog security,
    IClock clock, PasswordPolicyOptions passwordPolicy, ICurrentTenantSetter tenantScope)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        Guid? tenantId = null;
        if (!string.IsNullOrWhiteSpace(command.TenantIdentifier))
        {
            var slug = TenantSlug.Create(command.TenantIdentifier);
            tenantId = (await db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Slug == slug, ct)
                ?? throw new DomainException("AUTH-001", "Invalid credentials.")).Id;
            // Same reasoning as LoginHandler: the PASSWORD_CHANGED security event
            // is tenant-stamped and must be written under that tenant's context.
            tenantScope.Set(tenantId.Value);
        }

        var email = command.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);
        if (user is null || !user.IsActive || user.IsLockedOut(clock.UtcNow)
            || !hasher.Verify(user.PasswordHash, command.CurrentPassword))
        {
            throw new DomainException("AUTH-001", "Invalid credentials.");
        }

        // Reuse ban: the new password may not match the current hash or any retired one in scope.
        var history = await db.PasswordHistory
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.SetAtUtc)
            .Take(Math.Max(passwordPolicy.HistoryDepth, 0))
            .ToListAsync(ct);
        if (hasher.Verify(user.PasswordHash, command.NewPassword)
            || history.Any(h => hasher.Verify(h.PasswordHash, command.NewPassword)))
        {
            throw new DomainException(
                "AUTH-102", $"The new password must differ from the last {passwordPolicy.HistoryDepth + 1} passwords.");
        }

        db.PasswordHistory.Add(new PasswordHistoryEntry
        {
            UserId = user.Id,
            PasswordHash = user.PasswordHash,
            SetAtUtc = clock.UtcNow,
        });
        user.ChangePassword(hasher.Hash(command.NewPassword), clock.UtcNow);

        // Prune history beyond the configured depth.
        var stale = await db.PasswordHistory
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.SetAtUtc)
            .Skip(Math.Max(passwordPolicy.HistoryDepth, 0))
            .ToListAsync(ct);
        db.PasswordHistory.RemoveRange(stale);

        await security.WriteAsync("PASSWORD_CHANGED", tenantId, email, null, ct);
        await db.SaveChangesAsync(ct);
    }
}
