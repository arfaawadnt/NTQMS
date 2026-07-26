using FluentAssertions;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.DocumentControl;

/// <summary>
/// Controlled printed-copy / distribution register (F-11 / ISO 17025 §8.3):
/// a numbered physical copy issued to a holder, then returned or destroyed.
/// </summary>
public sealed class DocumentControlledCopyTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 27, 9, 0, 0, TimeSpan.Zero);

    private static DocumentControlledCopy Issued() =>
        DocumentControlledCopy.Issue(
            Guid.CreateVersion7(), "SOP-CAL-045", "2.0", 1, "Lab bench 3", Guid.CreateVersion7(), At);

    [Fact]
    public void Issue_records_the_holder_number_and_version()
    {
        var copy = Issued();
        copy.Status.Should().Be(ControlledCopyStatus.Issued);
        copy.CopyNumber.Should().Be(1);
        copy.Holder.Should().Be("Lab bench 3");
        copy.VersionLabel.Should().Be("2.0");
    }

    [Fact]
    public void A_holder_is_required()
    {
        var act = () => DocumentControlledCopy.Issue(
            Guid.CreateVersion7(), "SOP-1", "1.0", 1, " ", Guid.CreateVersion7(), At);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CCP-001");
    }

    [Fact]
    public void A_copy_can_be_returned_or_destroyed_once_and_then_is_immutable()
    {
        var copy = Issued();
        copy.Close(ControlledCopyStatus.Returned, Guid.CreateVersion7(), At);

        copy.Status.Should().Be(ControlledCopyStatus.Returned);
        copy.ClosedAtUtc.Should().Be(At);
        copy.DomainEvents.OfType<ControlledCopyClosed>().Should().ContainSingle();

        var again = () => copy.Close(ControlledCopyStatus.Destroyed, Guid.CreateVersion7(), At);
        again.Should().Throw<DomainException>().Which.Code.Should().Be("CCP-010");
    }

    [Fact]
    public void Closing_with_an_invalid_outcome_is_rejected()
    {
        var act = () => Issued().Close(ControlledCopyStatus.Issued, Guid.CreateVersion7(), At);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CCP-003");
    }
}
