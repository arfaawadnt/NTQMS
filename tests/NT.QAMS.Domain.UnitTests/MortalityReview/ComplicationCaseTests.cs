using FluentAssertions;
using NT.QAMS.Domain.MortalityReview;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.MortalityReview;

public class ComplicationCaseTests
{
    private static readonly Guid Reviewer = Guid.CreateVersion7();
    private static readonly DateTimeOffset When = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private static ComplicationCase Reported() =>
        ComplicationCase.Report("CMP-1", "PT-1", "Theatre", ComplicationType.ReturnToTheatre,
            ComplicationSeverity.Severe, When, "Bleeding, re-exploration.");

    [Fact]
    public void A_reported_complication_carries_its_type_and_severity()
    {
        var c = Reported();
        c.Type.Should().Be(ComplicationType.ReturnToTheatre);
        c.Severity.Should().Be(ComplicationSeverity.Severe);
        c.Status.Should().Be(ComplicationStatus.Reported);
        c.Preventable.Should().BeNull();
    }

    [Fact]
    public void Review_captures_the_preventability_judgement()
    {
        var c = Reported();
        c.RecordReview(Reviewer, "Avoidable with better haemostasis.", preventable: true, When);
        c.Status.Should().Be(ComplicationStatus.Reviewed);
        c.Preventable.Should().BeTrue();
    }

    [Fact]
    public void Review_requires_notes()
    {
        var c = Reported();
        var act = () => c.RecordReview(Reviewer, " ", true, When);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CMP-011");
    }

    [Fact]
    public void Close_requires_review_first()
    {
        var c = Reported();
        var early = c.Close;
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CMP-012");

        c.RecordReview(Reviewer, "Reviewed.", false, When);
        c.Close();
        c.Status.Should().Be(ComplicationStatus.Closed);
    }

    [Fact]
    public void A_patient_reference_is_required()
    {
        var act = () => ComplicationCase.Report("CMP-X", " ", "ICU", ComplicationType.UnplannedIcuAdmission,
            ComplicationSeverity.Moderate, When, "x");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CMP-001");
    }

    [Fact]
    public void A_case_can_be_rejected_and_a_closed_case_cannot()
    {
        // M-18: the correction path — a rejected case leaves the morbidity counts.
        var c = Reported();
        c.Reject(Reviewer, "Wrong patient.", When);
        c.Status.Should().Be(ComplicationStatus.Rejected);
        c.DomainEvents.OfType<ComplicationCaseRejected>().Should().ContainSingle();

        var closedCase = Reported();
        closedCase.RecordReview(Reviewer, "Confirmed.", true, When);
        closedCase.Close();
        var act = () => closedCase.Reject(Reviewer, "Too late.", When);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CMP-013");
    }
}
