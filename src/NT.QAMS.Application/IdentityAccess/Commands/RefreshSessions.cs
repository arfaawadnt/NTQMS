using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IdentityAccess.Commands;

/// <summary>Typed refresh-session configuration (Auth:RefreshTokenDays), validated at composition.</summary>
public sealed record RefreshSessionOptions(int Days)
{
    public RefreshSessionOptions Validated() =>
        Days > 0 ? this : throw new InvalidOperationException(
            "Auth:RefreshTokenDays must be a positive number of days.");

    public TimeSpan Lifetime => TimeSpan.FromDays(Days);
}

/// <summary>The refresh cookie's value and its expiry — set by the controller, never serialized to a body.</summary>
public sealed record RefreshGrant(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// A full sign-in/refresh outcome: the body the client sees plus the cookie
/// grant it must never see in script (ADR-0009). Grant is null for the
/// MFA-required intermediate and enrollment-scoped sessions.
/// </summary>
public sealed record LoginResult(AuthResponse Response, RefreshGrant? Refresh);

/// <summary>
/// Opaque token wire format: "<sessionId:N>.<base64url secret>". Only the
/// SHA-256 of the secret is stored; lookup is by session id, comparison by
/// hash — a database leak yields nothing replayable.
/// </summary>
public static class RefreshTokenFormat
{
    private const int SecretBytes = 32;

    public static (string Token, string Hash, Guid SessionId) Mint()
    {
        var sessionId = Guid.CreateVersion7();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return ($"{sessionId:N}.{secret}", Hash(secret), sessionId);
    }

    public static (Guid SessionId, string PresentedHash)? TryParse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var separator = token.IndexOf('.');
        if (separator <= 0 || separator == token.Length - 1)
        {
            return null;
        }

        return Guid.TryParseExact(token[..separator], "N", out var sessionId)
            ? (sessionId, Hash(token[(separator + 1)..]))
            : null;
    }

    private static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));
}

// ── Refresh ────────────────────────────────────────────────────────────────

/// <summary>
/// Rotates the presented refresh session (ADR-0009): a live token yields a
/// fresh access token + a NEW refresh token; the presented one is retired.
/// Presenting an already-rotated token is treated as theft — the entire
/// family is revoked and the event is written to the security ledger.
/// Anonymous by design: the httpOnly cookie IS the credential.
/// </summary>
[AllowUnauthenticated]
public sealed record RefreshTokenCommand(string? PresentedToken) : ICommand<LoginResult>;

public sealed class RefreshTokenHandler(
    IAppDbContext db, IJwtTokenService jwt, ISecurityEventLog security,
    IClock clock, RefreshSessionOptions options)
    : ICommandHandler<RefreshTokenCommand, LoginResult>
{
    public async Task<LoginResult> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var parsed = RefreshTokenFormat.TryParse(command.PresentedToken)
            ?? throw new DomainException("AUTH-009", "The session has expired. Please sign in again.");

        var session = await db.RefreshSessions
            .SingleOrDefaultAsync(s => s.Id == parsed.SessionId, ct);

        if (session is null || !string.Equals(session.TokenHash, parsed.PresentedHash, StringComparison.Ordinal))
        {
            await security.WriteAsync("REFRESH_INVALID", null, null, parsed.SessionId.ToString("N"), ct);
            throw new DomainException("AUTH-009", "The session has expired. Please sign in again.");
        }

        if (session.RevokedAtUtc is not null)
        {
            // Reuse of a retired link — the stolen-token tell. Kill the family.
            var family = await db.RefreshSessions
                .Where(s => s.FamilyId == session.FamilyId && s.RevokedAtUtc == null)
                .ToListAsync(ct);
            family.ForEach(s => s.Revoke(clock.UtcNow));
            await security.WriteAsync(
                "REFRESH_REUSE_DETECTED", null, null, $"family={session.FamilyId:N}", ct);
            await db.SaveChangesAsync(ct);
            throw new DomainException("AUTH-008", "The session has been revoked. Please sign in again.");
        }

        if (!session.IsLive(clock.UtcNow))
        {
            throw new DomainException("AUTH-009", "The session has expired. Please sign in again.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == session.UserId, ct);
        if (user is null || !user.IsActive)
        {
            session.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(ct);
            throw new DomainException("AUTH-006", "Your session is no longer valid. Please sign in again.");
        }

        // Rotate: successor in the same family; claims are re-read from the
        // CURRENT user record, so a role change propagates within one cycle.
        var (token, hash, sessionId) = RefreshTokenFormat.Mint();
        var successor = RefreshSession.Start(
            sessionId, user.Id, session.FamilyId, hash, clock.UtcNow, options.Lifetime);
        session.Rotate(successor.Id, clock.UtcNow);
        db.RefreshSessions.Add(successor);
        await db.SaveChangesAsync(ct);

        var (accessToken, expires) = jwt.Issue(user);
        return new LoginResult(
            new AuthResponse(accessToken, expires, user.Role.ToString(), user.DisplayName,
                user.TenantId, MfaRequired: false),
            new RefreshGrant(token, successor.ExpiresAtUtc));
    }
}

// ── Logout ─────────────────────────────────────────────────────────────────

/// <summary>
/// Revokes the presented session's whole family server-side (extends F-07
/// revocation to the refresh chain). Anonymous by design: the access token
/// may already be expired at logout; an absent/invalid cookie is a no-op.
/// </summary>
[AllowUnauthenticated]
public sealed record LogoutCommand(string? PresentedToken) : ICommand;

public sealed class LogoutHandler(IAppDbContext db, ISecurityEventLog security, IClock clock)
    : ICommandHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand command, CancellationToken ct)
    {
        var parsed = RefreshTokenFormat.TryParse(command.PresentedToken);
        if (parsed is null)
        {
            return;
        }

        var session = await db.RefreshSessions
            .SingleOrDefaultAsync(s => s.Id == parsed.Value.SessionId, ct);
        if (session is null
            || !string.Equals(session.TokenHash, parsed.Value.PresentedHash, StringComparison.Ordinal))
        {
            return;
        }

        var family = await db.RefreshSessions
            .Where(s => s.FamilyId == session.FamilyId && s.RevokedAtUtc == null)
            .ToListAsync(ct);
        family.ForEach(s => s.Revoke(clock.UtcNow));
        await security.WriteAsync("LOGOUT", null, null, $"family={session.FamilyId:N}", ct);
        await db.SaveChangesAsync(ct);
    }
}
