using FluentAssertions;
using NT.QAMS.Domain.MortalityReview;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.MortalityReview;

public class MortalityReviewTests
{
    private static readonly Guid R1 = Guid.CreateVersion7();
    private static readonly Guid R2 = Guid.CreateVersion7();
    private static readonly DateTimeOffset Died = new(2026, 9, 1, 3, 0, 0, TimeSpan.Zero);

    private static Domain.MortalityReview.MortalityReview Reported() =>
        Domain.MortalityReview.MortalityReview.Report("MRT-1", "PT-1", "ICU", Died, "Sepsis");

    [Fact]
    public void An_expected_death_needs_no_second_review_and_closes_from_classified()
    {
        var m = Reported();
        m.Classify(R1, DeathClassification.Expected, "End-stage disease, DNR in place.");
        m.RequiresSecondReview.Should().BeFalse();
        m.Close();
        m.Status.Should().Be(MortalityStatus.Closed);
    }

    [Fact]
    public void A_non_expected_death_requires_second_review_before_closure()
    {
        var m = Reported();
        m.Classify(R1, DeathClassification.PotentiallyPreventable, "Delayed escalation.");
        m.RequiresSecondReview.Should().BeTrue();

        var earlyClose = m.Close;
        earlyClose.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MRT-018");
    }

    [Fact]
    public void The_second_reviewer_must_differ_from_the_first()
    {
        var m = Reported();
        m.Classify(R1, DeathClassification.Unexpected, "Sudden deterioration.");

        var sameReviewer = () => m.RecordSecondReview(R1, "Concur.", concurs: true);
        sameReviewer.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-MRT-001");

        m.RecordSecondReview(R2, "Independent review — concur.", concurs: true);
        m.Status.Should().Be(MortalityStatus.SecondReviewed);
    }

    [Fact]
    public void An_expected_death_rejects_a_second_review()
    {
        var m = Reported();
        m.Classify(R1, DeathClassification.Expected, "Palliative.");
        var act = () => m.RecordSecondReview(R2, "x", true);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MRT-013");
    }

    [Fact]
    public void Full_non_expected_path_reaches_closed()
    {
        var m = Reported();
        m.Classify(R1, DeathClassification.Preventable, "Missed diagnosis.");
        m.RecordSecondReview(R2, "Concur — preventable.", concurs: true);
        m.MarkCommitteeDiscussed("Root cause addressed; CAPA raised.");
        m.Close();
        m.Status.Should().Be(MortalityStatus.Closed);
    }

    [Fact]
    public void Classification_requires_findings()
    {
        var m = Reported();
        var act = () => m.Classify(R1, DeathClassification.Unexpected, " ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MRT-011");
    }
}
