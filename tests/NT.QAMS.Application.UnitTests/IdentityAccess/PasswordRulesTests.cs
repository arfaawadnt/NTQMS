using FluentAssertions;
using NT.QAMS.Application.IdentityAccess;
using Xunit;

namespace NT.QAMS.Application.UnitTests.IdentityAccess;

/// <summary>
/// Password policy (F-15 / 21 CFR Part 11 §11.300(b)): a single set of rules —
/// length, character-class complexity, and offline breach screening — enforced
/// uniformly wherever a password is set.
/// </summary>
public sealed class PasswordRulesTests
{
    [Theory]
    [InlineData("Str0ng-Initial-Pass!")]
    [InlineData("Tenant-Admin-Pass-1!")]
    [InlineData("Analyst-Pass-1!")]
    [InlineData("aB3$aB3$aB3$")] // exactly the 12-char minimum, all four classes
    public void A_strong_password_is_accepted(string password)
    {
        PasswordRules.HasComplexity(password).Should().BeTrue();
        PasswordRules.NotCompromised(password).Should().BeTrue();
        password.Length.Should().BeGreaterThanOrEqualTo(PasswordRules.MinLength);
    }

    [Theory]
    [InlineData("alllowercase1!")]   // no upper-case
    [InlineData("ALLUPPERCASE1!")]   // no lower-case
    [InlineData("NoDigitsHere!!")]   // no digit
    [InlineData("NoSymbols1234")]    // no symbol
    public void A_password_missing_a_character_class_fails_complexity(string password) =>
        PasswordRules.HasComplexity(password).Should().BeFalse();

    [Theory]
    [InlineData("password")]
    [InlineData("Password123")]
    [InlineData("qwerty")]
    [InlineData("changeme")]
    [InlineData("qmsadmin")]
    public void A_common_or_breached_password_is_rejected(string password) =>
        PasswordRules.NotCompromised(password).Should().BeFalse();

    [Fact]
    public void The_minimum_length_is_twelve()
    {
        PasswordRules.MinLength.Should().Be(12);
    }
}
