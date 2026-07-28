using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NT.QAMS.Infrastructure.Configuration;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-5 finding CFG-001/002: a PRESENT-but-invalid configuration value
/// refuses startup instead of silently becoming the default — a mistyped
/// security policy must fail fast, loudly, at boot.
/// </summary>
public sealed class ConfigGuardTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value))
            .Build();

    [Fact]
    public void A_missing_key_falls_back_to_its_documented_default()
    {
        var config = Config();

        ConfigGuard.ReadInt(config, "PasswordPolicy:MaxAgeDays", 90).Should().Be(90);
        ConfigGuard.ReadBool(config, "Security:RequireMfaForPrivilegedRoles", false).Should().BeFalse();
        ConfigGuard.ReadDecimal(config, "AnalyticalQuality:Westgard:WarningSd", 2m).Should().Be(2m);
    }

    [Fact]
    public void A_valid_value_is_used()
    {
        var config = Config(("Jwt:ExpiryMinutes", "45"), ("Security:RequireMfaForPrivilegedRoles", "true"));

        ConfigGuard.ReadInt(config, "Jwt:ExpiryMinutes", 60).Should().Be(45);
        ConfigGuard.ReadBool(config, "Security:RequireMfaForPrivilegedRoles", false).Should().BeTrue();
    }

    [Theory]
    [InlineData("Jwt:ExpiryMinutes", "sixty")]
    [InlineData("PasswordPolicy:MaxAgeDays", "90 days")]
    public void An_invalid_integer_refuses_startup(string key, string bad)
    {
        var act = () => ConfigGuard.ReadInt(Config((key, bad)), key, 60);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{key}*", "the operator must see WHICH key is broken")
            .WithMessage("*Refusing to start*");
    }

    [Fact]
    public void An_invalid_boolean_refuses_startup_instead_of_weakening_policy()
    {
        var act = () => ConfigGuard.ReadBool(
            Config(("Security:RequireMfaForPrivilegedRoles", "treu")),
            "Security:RequireMfaForPrivilegedRoles", false);

        act.Should().Throw<InvalidOperationException>(
            "a typo must never silently disable an MFA policy");
    }
}
