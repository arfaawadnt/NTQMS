using Microsoft.Extensions.Configuration;

namespace NT.QAMS.Infrastructure.Configuration;

/// <summary>
/// CFG-001/002: fail-fast configuration reads. A MISSING key falls back to
/// its documented default; a PRESENT-BUT-INVALID value throws at startup —
/// a mistyped security or policy setting must never silently become the
/// default (e.g. "Securty:RequireMfa=treu" quietly disabling MFA policy).
/// </summary>
public static class ConfigGuard
{
    public static int ReadInt(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return int.TryParse(raw, out var value) ? value : throw Invalid(key, raw, "an integer");
    }

    public static bool ReadBool(IConfiguration configuration, string key, bool fallback)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return bool.TryParse(raw, out var value) ? value : throw Invalid(key, raw, "true/false");
    }

    public static decimal ReadDecimal(IConfiguration configuration, string key, decimal fallback)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return decimal.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Invalid(key, raw, "a decimal number");
    }

    private static InvalidOperationException Invalid(string key, string raw, string expected) =>
        new($"Configuration '{key}' has invalid value '{raw}' — expected {expected}. " +
            "Refusing to start rather than silently applying the default (CFG-002).");
}
