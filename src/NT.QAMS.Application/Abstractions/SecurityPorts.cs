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
    (string Token, DateTimeOffset ExpiresAtUtc) Issue(UserAccount user);
}

/// <summary>
/// Race-free business reference numbers (e.g. NC-2026-0007) from the per-tenant
/// per-type per-year counter table — never COUNT(*)+1.
/// </summary>
public interface IReferenceNumberGenerator
{
    Task<string> NextAsync(Guid tenantId, string refType, CancellationToken cancellationToken);
}
