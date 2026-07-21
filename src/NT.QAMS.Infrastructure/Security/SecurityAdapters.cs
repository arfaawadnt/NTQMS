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
    public int ExpiryMinutes { get; set; } = 120;
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
            ExpiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 120,
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

    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(UserAccount user)
    {
        var expires = _clock.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
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
