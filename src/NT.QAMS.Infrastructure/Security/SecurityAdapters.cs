using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Security;

/// <summary>ASP.NET Core Identity PBKDF2 hasher behind the application port.</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<UserAccount> _hasher = new();
    private static readonly UserAccount Dummy = null!;

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(Dummy, hash, password)
            != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "nt-qams";
    public string Audience { get; set; } = "nt-qams";
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// SEC-017 (ADR-0003): with the access token held in SPA web storage, a
    /// short lifetime bounds the exposure window of a stolen token. Raise via
    /// Jwt:ExpiryMinutes only with a documented risk acceptance.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;
}

/// <summary>
/// HS256 JWT issuance. Claims: sub, email, name, role, tenant_id (tenant users
/// only). The secret must be ≥ 32 chars — enforced at startup, not first use.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    public const string TenantClaim = "tenant_id";

    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IConfiguration configuration, IClock clock)
    {
        _options = new JwtOptions
        {
            Issuer = configuration["Jwt:Issuer"] ?? "nt-qams",
            Audience = configuration["Jwt:Audience"] ?? "nt-qams",
            Secret = configuration["Jwt:Secret"] ?? string.Empty,
            ExpiryMinutes = Configuration.ConfigGuard.ReadInt(configuration, "Jwt:ExpiryMinutes", 60),
        };
        _clock = clock;

        if (string.IsNullOrWhiteSpace(_options.Secret) || _options.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Secret must be configured with at least 32 characters (set the Jwt__Secret environment variable).");
        }

        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
            SecurityAlgorithms.HmacSha256);
    }

    /// <summary>Claim marking a session's scope: "full" or "mfa_enrollment".</summary>
    public const string ScopeClaim = "scope";
    public const string EnrollmentScope = "mfa_enrollment";
    public const string FullScope = "full";

    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(UserAccount user, bool enrollmentOnly = false)
    {
        var expires = _clock.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ScopeClaim, enrollmentOnly ? EnrollmentScope : FullScope),
        };

        if (user.TenantId is { } tenantId)
        {
            claims.Add(new Claim(TenantClaim, tenantId.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: _clock.UtcNow.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
