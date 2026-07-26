using NT.QAMS.Domain.IdentityAccess;

namespace NT.QAMS.Application.Abstractions;

/// <summary>Password hashing port — Infrastructure adapts ASP.NET Core Identity's hasher (PBKDF2).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

/// <summary>JWT issuance port.</summary>
public interface IJwtTokenService
{
    /// <param name="enrollmentOnly">
    /// When true the token carries scope=mfa_enrollment: the holder may only reach
    /// the MFA-enrollment endpoints until they enrol (F-04 enforcement).
    /// </param>
    (string Token, DateTimeOffset ExpiresAtUtc) Issue(UserAccount user, bool enrollmentOnly = false);
}

/// <summary>
/// Race-free business reference numbers (e.g. NC-2026-0007) from the per-tenant
/// per-type per-year counter table — never COUNT(*)+1.
/// </summary>
public interface IReferenceNumberGenerator
{
    Task<string> NextAsync(Guid tenantId, string refType, CancellationToken cancellationToken);
}
