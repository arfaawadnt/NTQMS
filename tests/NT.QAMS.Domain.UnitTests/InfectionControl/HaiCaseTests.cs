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
}
