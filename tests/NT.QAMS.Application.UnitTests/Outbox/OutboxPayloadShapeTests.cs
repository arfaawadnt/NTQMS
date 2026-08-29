using System.Text.Json;
using FluentAssertions;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Domain.MortalityReview;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Outbox;

/// <summary>
/// Audit finding N-11: the outbox serializes each domain event with Web JSON
/// defaults (camelCase) and the ledger/timeline reader deserializes the same
/// bytes back. That serialized shape is a contract — a silent rename of an event
/// property would break every historical row. These tests pin the shape and
/// prove a lossless round-trip through the exact options the outbox uses.
/// </summary>
public class OutboxPayloadShapeTests
{
    private static readonly JsonSerializerOptions OutboxOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void A_domain_event_round_trips_through_the_outbox_serializer()
    {
        var original = new IncidentClosed(
            Guid.CreateVersion7(), "INC-2026-0007", Guid.CreateVersion7());

        var json = JsonSerializer.Serialize(original, original.GetType(), OutboxOptions);
        var restored = (IncidentClosed)JsonSerializer.Deserialize(json, typeof(IncidentClosed), OutboxOptions)!;

        restored.Should().Be(original, "records round-trip by value through the outbox options");
    }

    [Fact]
    public void The_serialized_shape_is_camel_case_as_the_ledger_reader_expects()
    {
        var e = new MortalityClassified(
            Guid.CreateVersion7(), "MRT-2026-0003", "PotentiallyPreventable", Guid.CreateVersion7());

        var json = JsonSerializer.Serialize(e, e.GetType(), OutboxOptions);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("mortalityReviewId", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("reviewRef", out _).Should().BeTrue();
        doc.RootElement.GetProperty("classification").GetString().Should().Be("PotentiallyPreventable");
        doc.RootElement.TryGetProperty("reviewerId", out _).Should().BeTrue();
        // Guard against a PascalCase regression that would orphan historical rows.
        doc.RootElement.TryGetProperty("MortalityReviewId", out _).Should().BeFalse();
    }
}
