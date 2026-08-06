using FluentAssertions;
using NT.QAMS.Application.Compliance;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Compliance;

/// <summary>
/// The signature content hash binds a Part 11 signature to the exact record state
/// it attests to (§11.70). It must be deterministic (so a signature can be
/// re-verified) and collision-resistant across field layouts (so a signature can
/// never be silently rebound to a different set of facts).
/// </summary>
public class SignatureContentHashTests
{
    [Fact]
    public void Same_fields_in_same_order_produce_the_same_hash()
    {
        var a = SignatureContentHash.Compute(("nc", "NC-1"), ("outcome", "passed"));
        var b = SignatureContentHash.Compute(("nc", "NC-1"), ("outcome", "passed"));

        a.Should().Be(b);
        a.Should().MatchRegex("^[0-9a-f]{64}$", "it is a lower-case SHA-256 hex digest");
    }

    [Fact]
    public void A_changed_value_changes_the_hash()
    {
        var passed = SignatureContentHash.Compute(("nc", "NC-1"), ("outcome", "passed"));
        var failed = SignatureContentHash.Compute(("nc", "NC-1"), ("outcome", "not-passed"));

        passed.Should().NotBe(failed);
    }

    [Fact]
    public void Field_order_is_significant()
    {
        var forward = SignatureContentHash.Compute(("a", "1"), ("b", "2"));
        var swapped = SignatureContentHash.Compute(("b", "2"), ("a", "1"));

        forward.Should().NotBe(swapped);
    }

    [Fact]
    public void Null_is_distinguishable_from_empty_and_from_the_literal_marker()
    {
        var nullValue = SignatureContentHash.Compute(("x", null));
        var emptyValue = SignatureContentHash.Compute(("x", string.Empty));
        var literalMarker = SignatureContentHash.Compute(("x", "n:"));

        nullValue.Should().NotBe(emptyValue);
        nullValue.Should().NotBe(literalMarker);
    }

    [Fact]
    public void A_value_containing_a_delimiter_cannot_forge_a_different_layout()
    {
        // "a=x" packed into one field must not hash-collide with two fields a=<empty>, x.
        var packed = SignatureContentHash.Compute(("field", "a=x"));
        var split = SignatureContentHash.Compute(("field", "a"), ("x", string.Empty));

        packed.Should().NotBe(split);
    }
}
