using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IdentityAccess.Commands;

// ── MFA enrollment ───────────────────────────────────────────────────────────

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

// ── E-signature PIN ──────────────────────────────────────────────────────────

public sealed record SetPinCommand(string Pin) : ICommand;

public sealed class SetPinValidator : AbstractValidator<SetPinCommand>
{
    public SetPinValidator() =>
        RuleFor(x => x.Pin).NotEmpty().Matches("^[0-9]{4}$")
            .WithMessage("The e-signature PIN must be exactly 4 digits.");
}

public sealed class SetPinHandler(IAppDbContext db, ICurrentUser user, IPasswordHasher hasher)
    : ICommandHandler<SetPinCommand>
{
    public async Task Handle(SetPinCommand c, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var account = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("USER-404", "User not found.");

        // Reuse the PBKDF2 password hasher for the PIN (salted, slow).
        account.SetPin(hasher.Hash(c.Pin));
        await db.SaveChangesAsync(ct);
    }
}
