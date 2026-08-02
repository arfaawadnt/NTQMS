using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IdentityAccess.Commands;

// â”€â”€ MFA enrollment â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireAuthenticatedActor]
public sealed record EnrollMfaCommand : ICommand<MfaEnrollmentResponse>;

public sealed class EnrollMfaHandler(
    IAppDbContext db, ICurrentUser user, ITotpService totp)
    : ICommandHandler<EnrollMfaCommand, MfaEnrollmentResponse>
{
    public async Task<MfaEnrollmentResponse> Handle(EnrollMfaCommand c, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var account = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("USER-404", "User not found.");

        var secret = totp.GenerateSecret();
        account.EnrollMfa(secret);
        await db.SaveChangesAsync(ct);

        return new MfaEnrollmentResponse(
            secret, totp.BuildOtpAuthUri(secret, account.Email, "NT.QAMS"));
    }
}

[RequireAuthenticatedActor]
public sealed record ConfirmMfaCommand(string Code) : ICommand;

public sealed class ConfirmMfaHandler(
    IAppDbContext db, ICurrentUser user, ITotpService totp, ISecurityEventLog security, IClock clock)
    : ICommandHandler<ConfirmMfaCommand>
{
    public async Task Handle(ConfirmMfaCommand c, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var account = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("USER-404", "User not found.");

        if (string.IsNullOrWhiteSpace(account.MfaSecret) || !totp.Verify(account.MfaSecret, c.Code, clock.UtcNow))
        {
            throw new DomainException("MFA-003", "The verification code is invalid.");
        }

        account.ConfirmMfa();
        await db.SaveChangesAsync(ct);
        await security.WriteAsync("MFA_ENABLED", account.TenantId, account.Email, null, ct);
    }
}

// â”€â”€ E-signature PIN â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>
/// Sets or changes the caller's e-signature PIN. The account password is
/// re-verified even inside a live session: the PIN is one of the two Part 11
/// §11.200(a)(1) signing components, and a component that a hijacked session
/// could silently replace by holding only the other would not be a component.
/// </summary>
[RequireAuthenticatedActor]
public sealed record SetPinCommand(string CurrentPassword, string Pin) : ICommand;

public sealed class SetPinValidator : AbstractValidator<SetPinCommand>
{
    public SetPinValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.Pin).NotEmpty().Matches("^[0-9]{4}$")
            .WithMessage("The e-signature PIN must be exactly 4 digits.");
    }
}

public sealed class SetPinHandler(
    IAppDbContext db, ICurrentUser user, IPasswordHasher hasher, ISecurityEventLog security, ICurrentTenant tenant)
    : ICommandHandler<SetPinCommand>
{
    public async Task Handle(SetPinCommand c, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var account = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("USER-404", "User not found.");

        if (!hasher.Verify(account.PasswordHash, c.CurrentPassword))
        {
            await security.WriteAsync("PIN_CHANGE_DENIED", tenant.TenantId, account.DisplayName, "bad-password", ct);
            throw new DomainException("AUTH-001", "Invalid credentials.");
        }

        var isChange = !string.IsNullOrWhiteSpace(account.PinHash);

        // Reuse the PBKDF2 password hasher for the PIN (salted, slow).
        account.SetPin(hasher.Hash(c.Pin));
        await security.WriteAsync(
            isChange ? "PIN_CHANGED" : "PIN_SET", tenant.TenantId, account.DisplayName, "self-service", ct);
        await db.SaveChangesAsync(ct);
    }
}
