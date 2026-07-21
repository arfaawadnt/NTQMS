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
/// map to 401 and never reveal which factor failed. Every attempt — success,
/// bad password, bad MFA, lockout — is written to the security-event ledger.
/// </summary>
public sealed record LoginCommand(string? TenantIdentifier, string Email, string Password, string? MfaCode)
    : ICommand<AuthResponse>;

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
    ITotpService totp, ISecurityEventLog security, IClock clock)
    : ICommandHandler<LoginCommand, AuthResponse>
{
    private const string InvalidCredentials = "Invalid credentials.";

    public async Task<AuthResponse> Handle(LoginCommand command, CancellationToken ct)
    {
        Guid? tenantId = null;

        if (!string.IsNullOrWhiteSpace(command.TenantIdentifier))
        {
            var slug = TenantSlug.Create(command.TenantIdentifier);
            var tenant = await db.Tenants.AsNoTracking()
                .SingleOrDefaultAsync(t => t.Slug == slug, ct)
                ?? throw await FailAsync("AUTH-001", InvalidCredentials, null, command.Email, "unknown-tenant", ct);

            if (tenant.Status != TenantStatus.Active)
            {
                throw await FailAsync("AUTH-002", "This tenant is not active.", tenant.Id, command.Email, "tenant-inactive", ct);
            }

            tenantId = tenant.Id;
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

        if (user.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(command.MfaCode))
            {
                // Password OK but MFA required — signal the client to collect a code.
                await security.WriteAsync("LOGIN_MFA_REQUIRED", tenantId, email, null, ct);
                return new AuthResponse(string.Empty, default, user.Role.ToString(), user.DisplayName, user.TenantId, MfaRequired: true);
            }

            if (!totp.Verify(user.MfaSecret!, command.MfaCode, clock.UtcNow))
            {
                user.RegisterFailedLogin(clock.UtcNow);
                await db.SaveChangesAsync(ct);
                throw await FailAsync("AUTH-005", InvalidCredentials, tenantId, email, "bad-mfa", ct);
            }
        }

        user.RegisterSuccessfulLogin();
        await db.SaveChangesAsync(ct);
        await security.WriteAsync("LOGIN_SUCCESS", tenantId, email, null, ct);

        var (token, expires) = jwt.Issue(user);
        return new AuthResponse(token, expires, user.Role.ToString(), user.DisplayName, user.TenantId, MfaRequired: false);
    }

    private async Task<DomainException> FailAsync(
        string code, string message, Guid? tenantId, string email, string reason, CancellationToken ct)
    {
        await security.WriteAsync("LOGIN_FAILED", tenantId, email, reason, ct);
        return new DomainException(code, message);
    }
}
