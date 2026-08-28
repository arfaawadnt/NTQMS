using FluentAssertions;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.WebApi.Controllers;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>The boundary enum conversion's contract (M-11) — see <see cref="RequestEnum"/>.</summary>
public class RequestEnumTests
{
    [Theory]
    [InlineData("Fall")]
    [InlineData("fall")]
    [InlineData("FALL")]
    public void A_defined_name_parses_case_insensitively(string value) =>
        RequestEnum.Parse<IncidentCategory>(value).Should().Be(IncidentCategory.Fall);

    [Fact]
    public void An_unknown_name_throws_naming_the_enum()
    {
        var act = () => RequestEnum.Parse<HarmGrade>("Catastrophic-Typo");
        act.Should().Throw<ArgumentException>().WithMessage("*not a valid HarmGrade*");
    }

    [Fact]
    public void A_numeric_string_outside_the_defined_values_is_rejected_not_smuggled()
    {
        // Enum.Parse would return (HarmGrade)7777 here — an undefined value
        // that only dies at the database CHECK constraint, mid-transaction.
        var act = () => RequestEnum.Parse<HarmGrade>("7777");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_numeric_string_for_a_defined_value_still_parses()
    {
        var numeric = ((int)IncidentCategory.Fall).ToString();
        RequestEnum.Parse<IncidentCategory>(numeric).Should().Be(IncidentCategory.Fall);
    }
}
