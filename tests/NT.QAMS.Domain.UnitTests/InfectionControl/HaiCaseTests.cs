using FluentAssertions;
using NT.QAMS.Domain.InfectionControl;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.InfectionControl;

public class HaiCaseTests
{
    private static readonly Guid Reviewer = Guid.CreateVersion7();
    private static readonly DateTimeOffset When = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(HaiType.Clabsi, DeviceType.CentralLine)]
    [InlineData(HaiType.Cauti, DeviceType.UrinaryCatheter)]
    [InlineData(HaiType.Vap, DeviceType.Ventilator)]
    public void A_device_associated_case_maps_to_its_device(HaiType type, DeviceType device)
    {
        var c = HaiCase.Report("HAI-1", type, "PT-1", "ICU", When, "E. coli", "x");
        c.AssociatedDevice.Should().Be(device);
    }

    [Fact]
    public void An_ssi_has_no_associated_device()
    {
        var c = HaiCase.Report("HAI-2", HaiType.Ssi, "PT-2", "Theatre", When, null, "Post-op");
        c.AssociatedDevice.Should().BeNull();
        c.Organism.Should().BeNull();
    }

    [Fact]
    public void Report_requires_a_patient_reference()
    {
        var act = () => HaiCase.Report("HAI-3", HaiType.Clabsi, " ", "ICU", When, null, "x");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("HAI-001");
    }

    [Fact]
    public void Review_requires_notes_and_only_from_reported()
    {
        var c = HaiCase.Report("HAI-4", HaiType.Cauti, "PT-4", "Ward A", When, null, "x");

        var noNotes = () => c.RecordReview(Reviewer, " ", When);
        noNotes.Should().Throw<DomainException>().Which.Code.Should().Be("HAI-011");

        c.RecordReview(Reviewer, "Catheter removed; bundle reinforced.", When);
        c.Status.Should().Be(HaiStatus.Reviewed);

        var again = () => c.RecordReview(Reviewer, "x", When);
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("HAI-010");
    }

    [Fact]
    public void Close_requires_review_first()
    {
        var c = HaiCase.Report("HAI-5", HaiType.Vap, "PT-5", "ICU", When, "MRSA", "x");

        var early = c.Close;
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("HAI-012");

        c.RecordReview(Reviewer, "Reviewed.", When);
        c.Close();
        c.Status.Should().Be(HaiStatus.Closed);
    }

    [Fact]
    public void A_case_can_be_rejected_with_a_reason_from_reported_or_reviewed()
    {
        // M-18: the correction path — a rejected case leaves the official rates.
        var c = HaiCase.Report("HAI-3", HaiType.Clabsi, "PT-3", "ICU", When, null, "duplicate entry");
        c.Reject(Reviewer, "Duplicate of HAI-2.", When);

        c.Status.Should().Be(HaiStatus.Rejected);
        c.RejectedBy.Should().Be(Reviewer);
        c.RejectionReason.Should().Be("Duplicate of HAI-2.");
        c.DomainEvents.OfType<HaiCaseRejected>().Should().ContainSingle();
    }

    [Fact]
    public void A_rejection_requires_a_reason_and_a_closed_case_cannot_be_rejected()
    {
        var c = HaiCase.Report("HAI-4", HaiType.Cauti, "PT-4", "Ward", When, null, "x");
        var noReason = () => c.Reject(Reviewer, " ", When);
        noReason.Should().Throw<DomainException>().Which.Code.Should().Be("HAI-014");

        c.RecordReview(Reviewer, "Confirmed.", When);
        c.Close();
        var closed = () => c.Reject(Reviewer, "Too late.", When);
        closed.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("HAI-013");
    }
}
