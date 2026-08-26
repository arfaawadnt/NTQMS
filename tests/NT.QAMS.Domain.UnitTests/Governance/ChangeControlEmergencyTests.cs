using FluentAssertions;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Governance;

/// <summary>
/// HQMS M18 completion: the emergency-change pathway (implemented → ratified retrospectively by a
/// deadline) and impact-based routing (a high-impact change cannot be self-approved).
/// </summary>
public class ChangeControlEmergencyTests
{
    private static readonly Guid Proposer = Guid.CreateVersion7();
    private static readonly Guid Other = Guid.CreateVersion7();
    private static readonly Guid Risk = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Deadline = new(2026, 9, 8);

    [Fact]
    public void An_emergency_change_starts_implemented_pending_ratification()
    {
        var c = ChangeRequest.ProposeEmergency("CHG-1", "Failover to backup analyser", "Line down", Proposer, Deadline);
        c.Status.Should().Be(ChangeStatus.ImplementedPendingRatification);
        c.IsEmergency.Should().BeTrue();
        c.ImpactLevel.Should().Be(ChangeImpactLevel.High);
        c.RetrospectiveDeadline.Should().Be(Deadline);
    }

    [Fact]
    public void Ratification_needs_a_retrospective_risk_and_an_independent_ratifier()
    {
        var c = ChangeRequest.ProposeEmergency("CHG-2", "Emergency reagent swap", "Stockout", Proposer, Deadline);

        var noRisk = () => c.Ratify(Other, "Documented after the fact.", Now);
        noRisk.Should().Throw<DomainException>().Which.Code.Should().Be("CHG-031");

        c.LinkRiskAssessment(Risk);

        var selfRatify = () => c.Ratify(Proposer, "notes", Now);
        selfRatify.Should().Throw<DomainException>().Which.Code.Should().Be("CHG-032");

        c.Ratify(Other, "Reviewed and confirmed.", Now);
        c.Status.Should().Be(ChangeStatus.Closed);
        c.RatifiedBy.Should().Be(Other);
    }

    [Fact]
    public void A_ratified_emergency_change_can_then_pass_post_implementation_review()
    {
        var c = ChangeRequest.ProposeEmergency("CHG-3", "x", "y", Proposer, Deadline);
        c.LinkRiskAssessment(Risk);
        c.Ratify(Other, "notes", Now);
        c.RecordPostImplementationReview(Other, effective: true, "No adverse effect.", Now);
        c.Status.Should().Be(ChangeStatus.Reviewed);
    }

    [Fact]
    public void Ratification_is_overdue_past_the_deadline()
    {
        var c = ChangeRequest.ProposeEmergency("CHG-4", "x", "y", Proposer, Deadline);
        c.IsRatificationOverdue(Deadline).Should().BeFalse("the deadline day is still on time");
        c.IsRatificationOverdue(Deadline.AddDays(1)).Should().BeTrue();

        c.LinkRiskAssessment(Risk);
        c.Ratify(Other, "notes", Now);
        c.IsRatificationOverdue(Deadline.AddDays(30)).Should().BeFalse("a ratified change is no longer overdue");
    }

    [Fact]
    public void A_normal_change_cannot_be_ratified()
    {
        var c = ChangeRequest.Propose("CHG-5", "Normal", "impact", Proposer);
        var act = () => c.Ratify(Other, "notes", Now);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("CHG-030");
    }

    [Fact]
    public void A_high_impact_change_cannot_be_self_approved()
    {
        var c = ChangeRequest.Propose("CHG-6", "Risky", "impact", Proposer, ChangeImpactLevel.High);
        c.LinkRiskAssessment(Risk);

        var selfApprove = () => c.Approve(Proposer, Now);
        selfApprove.Should().Throw<DomainException>().Which.Code.Should().Be("CHG-016");

        c.Approve(Other, Now);
        c.Status.Should().Be(ChangeStatus.Approved);
    }

    [Fact]
    public void A_medium_impact_change_may_be_self_approved()
    {
        var c = ChangeRequest.Propose("CHG-7", "Routine", "impact", Proposer); // defaults to Medium
        c.LinkRiskAssessment(Risk);
        c.Approve(Proposer, Now);
        c.Status.Should().Be(ChangeStatus.Approved);
    }
}
