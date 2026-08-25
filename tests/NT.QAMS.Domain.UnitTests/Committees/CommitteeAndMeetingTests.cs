using FluentAssertions;
using NT.QAMS.Domain.Committees;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Committees;

public class CommitteeAndMeetingTests
{
    private static readonly Guid U1 = Guid.CreateVersion7();
    private static readonly Guid U2 = Guid.CreateVersion7();
    private static readonly Guid Chair = Guid.CreateVersion7();
    private static readonly DateTimeOffset When = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static Committee NewCommittee(int quorum = 2)
    {
        var c = Committee.Create("Quality & Safety Committee", "Oversees quality and patient safety.", CommitteeFrequency.Monthly, quorum);
        return c;
    }

    // ── Committee ────────────────────────────────────────────────────────────

    [Fact]
    public void Quorum_must_be_at_least_one()
    {
        var act = () => Committee.Create("C", "ToR", CommitteeFrequency.Monthly, 0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CMT-003");
    }

    [Fact]
    public void Cannot_add_the_same_member_twice()
    {
        var c = NewCommittee();
        c.AddMember(U1, "Chair");
        var act = () => c.AddMember(U1, "Member");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CMT-012");
    }

    [Fact]
    public void A_disbanded_committee_cannot_be_modified()
    {
        var c = NewCommittee();
        c.Disband();
        var act = () => c.AddMember(U1, "Member");
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CMT-015");
    }

    // ── Meeting ──────────────────────────────────────────────────────────────

    private static Meeting Quorate()
    {
        var m = Meeting.Schedule(Guid.CreateVersion7(), "MTG-2026-0001", When);
        m.RecordAttendance(U1, present: true);
        m.RecordAttendance(U2, present: true);
        return m;
    }

    [Fact]
    public void Cannot_hold_a_meeting_that_is_not_quorate()
    {
        var m = Meeting.Schedule(Guid.CreateVersion7(), "MTG-1", When);
        m.RecordAttendance(U1, present: true);
        m.RecordAttendance(U2, present: false);

        var act = () => m.Hold(committeeQuorum: 2);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MTG-014");
    }

    [Fact]
    public void Hold_when_quorate_transitions_to_held_and_raises_event()
    {
        var m = Quorate();
        m.Hold(committeeQuorum: 2);

        m.Status.Should().Be(MeetingStatus.Held);
        m.PresentCount.Should().Be(2);
        m.DomainEvents.OfType<MeetingHeld>().Should().ContainSingle();
    }

    [Fact]
    public void Decisions_can_only_be_added_after_the_meeting_is_held()
    {
        var m = Quorate();
        var early = () => m.AddDecision("Do X", U1, new DateOnly(2026, 10, 1));
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MTG-015");

        m.Hold(2);
        var decisionId = m.AddDecision("Do X", U1, new DateOnly(2026, 10, 1));
        m.Decisions.Single().Status.Should().Be(DecisionStatus.Open);

        m.CloseDecision(decisionId, "Completed");
        m.Decisions.Single().Status.Should().Be(DecisionStatus.Closed);
    }

    [Fact]
    public void Minutes_must_be_recorded_before_approval()
    {
        var m = Quorate();
        m.Hold(2);

        var noMinutes = () => m.ApproveMinutes(Chair);
        noMinutes.Should().Throw<DomainException>().Which.Code.Should().Be("MTG-022");

        m.RecordMinutes("Full and accurate minutes of the meeting.");
        m.ApproveMinutes(Chair);

        m.Status.Should().Be(MeetingStatus.MinutesApproved);
        m.MinutesApprovedBy.Should().Be(Chair);
        m.DomainEvents.OfType<MeetingMinutesApproved>().Should().ContainSingle();
    }

    [Fact]
    public void A_held_meeting_cannot_be_cancelled()
    {
        var m = Quorate();
        m.Hold(2);
        var act = m.Cancel;
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MTG-023");
    }
}
