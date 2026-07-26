using FluentAssertions;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.DocumentControl;

/// <summary>
/// Read-and-understand receipt (F-11 / ISO 9001 §7.5, 17025 §8.3): a durable,
/// version-pinned record that a person confirmed a specific controlled version.
/// </summary>
public sealed class DocumentAcknowledgementTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Records_the_user_version_and_instant_and_raises_an_event()
    {
        var docId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var ack = DocumentAcknowledgement.Record(docId, "SOP-CAL-045", "2.1", userId, At);

        ack.DocumentId.Should().Be(docId);
        ack.DocumentCode.Should().Be("SOP-CAL-045");
        ack.VersionLabel.Should().Be("2.1");
        ack.UserId.Should().Be(userId);
        ack.AcknowledgedAtUtc.Should().Be(At);
        ack.DomainEvents.OfType<DocumentAcknowledged>().Should().ContainSingle()
            .Which.VersionLabel.Should().Be("2.1");
    }

    [Fact]
    public void A_version_label_is_required()
    {
        var act = () => DocumentAcknowledgement.Record(
            Guid.CreateVersion7(), "SOP-1", " ", Guid.CreateVersion7(), At);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ACK-003");
    }

    [Fact]
    public void An_acknowledging_user_is_required()
    {
        var act = () => DocumentAcknowledgement.Record(
            Guid.CreateVersion7(), "SOP-1", "1.0", Guid.Empty, At);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ACK-002");
    }
}
