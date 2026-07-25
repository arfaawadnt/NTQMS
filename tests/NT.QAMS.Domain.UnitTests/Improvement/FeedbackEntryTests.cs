using FluentAssertions;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Improvement;

public sealed class FeedbackEntryTests
{
    private static readonly Guid Staff = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 7, 25);

    private static FeedbackEntry Entry(FeedbackType type = FeedbackType.Suggestion, int? score = 4) =>
        FeedbackEntry.Log("FB-2026-0001", "Customer", "Survey", type,
            "Faster result delivery", "Portal results could publish an hour earlier.", score, Today, Staff);

    [Fact]
    public void Score_is_bounded_one_to_five()
    {
        var tooHigh = () => Entry(score: 6);
        tooHigh.Should().Throw<DomainException>().Which.Code.Should().Be("FBK-003");
        Entry(score: null).SatisfactionScore.Should().BeNull();
    }

    [Fact]
    public void Lifecycle_runs_logged_reviewed_closed_with_required_narratives()
    {
        var feedback = Entry();
        var earlyClose = () => feedback.Close("done");
        earlyClose.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("FBK-012");

        feedback.Review("Feasible — checked with IT, publish job can move to 06:00.");
        var blank = () => feedback.Close(" ");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be("FBK-013");

        feedback.Close("Publish job rescheduled to 06:00 from August.");
        feedback.Status.Should().Be(FeedbackStatus.Closed);
    }

    [Fact]
    public void Only_dissatisfaction_escalates_and_escalation_is_terminal()
    {
        var suggestion = Entry(FeedbackType.Suggestion);
        var wrongType = () => suggestion.Escalate(Guid.CreateVersion7());
        wrongType.Should().Throw<DomainException>().Which.Code.Should().Be("FBK-014");

        var dissatisfaction = Entry(FeedbackType.Dissatisfaction, score: 1);
        var complaintId = Guid.CreateVersion7();
        dissatisfaction.Escalate(complaintId);
        dissatisfaction.Status.Should().Be(FeedbackStatus.Escalated);
        dissatisfaction.ComplaintId.Should().Be(complaintId);

        var again = () => dissatisfaction.Escalate(Guid.CreateVersion7());
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("FBK-015");
    }
}
