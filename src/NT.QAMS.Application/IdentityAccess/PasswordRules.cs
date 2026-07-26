using FluentValidation;

namespace NT.QAMS.Application.IdentityAccess;

/// <summary>
/// The single source of truth for password strength (21 CFR Part 11 §11.300(b) /
/// NIST SP 800-63B). Every place a password is set — user registration, admin
/// reset, tenant provisioning, self-service change — applies <see cref="StrongPassword"/>
/// so the rules can never drift apart (the audit found 10 vs 12 char minimums and
/// no complexity or breach screening). Screening is offline (a bundled list of the
/// most common/compromised passwords) so it adds no external dependency; an online
/// HIBP range check can be layered on later without changing call sites.
/// </summary>
public static class PasswordRules
{
    /// <summary>Minimum length, applied uniformly. NIST floor is 8; regulated systems use 12.</summary>
    public const int MinLength = 12;

    /// <summary>Upper bound guards against denial-of-service via unbounded hashing input.</summary>
    public const int MaxLength = 200;

    /// <summary>
    /// The most common / breach-list passwords, screened case-insensitively. Not
    /// exhaustive — it rejects the passwords attackers try first, which is the point
    /// of §11.300(b) "loss management" for identification codes.
    /// </summary>
    private static readonly HashSet<string> Compromised = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "passw0rd", "p@ssw0rd", "p@ssword",
        "123456", "1234567", "12345678", "123456789", "1234567890", "12345678910",
        "qwerty", "qwertyuiop", "qwerty123", "1q2w3e4r", "1q2w3e4r5t", "1qaz2wsx",
        "letmein", "welcome", "welcome1", "welcome123", "admin", "administrator",
        "admin123", "root", "toor", "changeme", "changeme1", "default", "secret",
        "iloveyou", "sunshine", "princess", "dragon", "monkey", "football",
        "baseball", "superman", "trustno1", "master", "shadow", "abc123",
        "abcd1234", "aa123456", "123qwe", "zaq12wsx", "qazwsx", "passwordadmin",
        "test", "test123", "testing", "temp", "temp123", "guest", "guest123",
        "labadmin", "quality", "quality1", "qms", "qmsadmin",
    };

    /// <summary>
    /// Applies the full policy: length, character-class complexity, and breach
    /// screening. Reusable on any string rule in a FluentValidation validator.
    /// </summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinLength)
                .WithMessage($"The password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength)
            .Must(HasComplexity)
                .WithMessage("The password must include upper- and lower-case letters, a digit, and a symbol.")
            .Must(NotCompromised)
                .WithMessage("This password is too common or appears in known breach lists. Choose another.");

    /// <summary>True when the password draws on all four character classes.</summary>
    public static bool HasComplexity(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        bool upper = false, lower = false, digit = false, symbol = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) { upper = true; }
            else if (char.IsLower(c)) { lower = true; }
            else if (char.IsDigit(c)) { digit = true; }
            else { symbol = true; }
        }

        return upper && lower && digit && symbol;
    }

    /// <summary>True when the password is not on the compromised/common list.</summary>
    public static bool NotCompromised(string? password) =>
        !string.IsNullOrEmpty(password) && !Compromised.Contains(password.Trim());
}
