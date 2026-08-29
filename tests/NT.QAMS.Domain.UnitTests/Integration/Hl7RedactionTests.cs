using FluentAssertions;
using NT.QAMS.Domain.Integration;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Integration;

/// <summary>
/// M-12 / ADR-0011: patient identifiers in a raw HL7 payload are masked before
/// storage. The message STRUCTURE survives (troubleshooting value); the direct
/// identifiers in the PID segment do not.
/// </summary>
public class Hl7RedactionTests
{
    private const string Sample =
        "MSH|^~\\&|HIS|HOSP|QMS|HOSP|20260901120000||ADT^A01|MSG0001|P|2.5\r"
        + "EVN|A01|20260901120000\r"
        + "PID|1||MRN123456^^^HOSP^MR||Doe^John^A||19800101|M|||12 Main St^^Amman^^11118||+962790000000\r"
        + "PV1|1|I|ICU^101^1";

    [Fact]
    public void The_pid_direct_identifiers_are_masked()
    {
        var masked = Hl7Redaction.MaskPatientIdentifiers(Sample);

        masked.Should().NotContain("MRN123456", "the medical record number is a direct identifier");
        masked.Should().NotContain("Doe^John", "the patient name is masked");
        masked.Should().NotContain("19800101", "the date of birth is masked");
        masked.Should().NotContain("12 Main St", "the address is masked");
        masked.Should().NotContain("+962790000000", "the phone number is masked");
    }

    [Fact]
    public void The_message_structure_and_other_segments_survive()
    {
        var masked = Hl7Redaction.MaskPatientIdentifiers(Sample);

        masked.Should().Contain("MSH|^~\\&|HIS|HOSP", "the header is untouched");
        masked.Should().Contain("PV1|1|I|ICU^101^1", "non-PID segments are untouched");
        masked.Should().StartWith("MSH|");
        masked.Split('\r').Should().HaveCount(4, "segment count is preserved");
        masked.Should().Contain("PID|1|", "the PID segment and its delimiters remain");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an hl7 message at all")]
    [InlineData("PID|")]
    public void Malformed_or_empty_input_is_returned_without_throwing(string input)
    {
        var act = () => Hl7Redaction.MaskPatientIdentifiers(input);
        act.Should().NotThrow();
    }
}
