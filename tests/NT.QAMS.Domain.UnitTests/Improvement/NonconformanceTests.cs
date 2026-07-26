using FluentAssertions;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Improvement;

public class NonconformanceTests
{
    private static readonly Guid Raiser = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly DateOnly Due = new(2026, 8, 15);
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static Nonconformance Raised()
    {
        var nc = Nonconformance.Raise(
            "NC-2026-0001", "Balance temp deviation", "Calibration balance out of range",
            4, 3, NcSourceType.Internal, Raiser);
        nc.Submit();
        return nc;
    }

    [Fact]
    public void Raise_computes_rpn_and_starts_in_draft()
    {
        var nc = Nonconformance.Raise("NC-2026-0001", "T", "D", 4, 3, NcSourceType.Audit, Raiser);

        nc.Status.Should().Be(NcStatus.Draft);
        nc.Rpn.Should().Be(12);
        nc.DomainEvents.Should().BeEmpty("NcRaised fires on submit, not on draft");
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 6)]
    public void Raise_rejects_out_of_range_scores(int severity, int likelihood)
    {
        var act = () => Nonconformance.Raise("R", "T", "D", severity, likelihood, NcSourceType.Internal, Raiser);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("NC-002");
    }

    [Fact]
    public void Full_happy_path_reaches_closed()
    {
        var nc = Raised();
        nc.Triage(Manager);
        nc.RecordRca(RcaMethod.FiveWhys, "Root cause: worn seal", Manager);
        var actionId = nc.PlanCapaAction(CapaActionType.Corrective, "Replace seal", Owner, Due);
        nc.CompleteCapaAction(actionId, Now);
        nc.SubmitForVerification();
        nc.Verify(passed: true, actorId: Manager);
        nc.ConfirmEffectiveness(effective: true, actorId: Manager);

        nc.Status.Should().Be(NcStatus.Closed);
        nc.DomainEvents.OfType<NcClosed>().Should().ContainSingle();
    }

    [Fact]
    public void Cannot_submit_for_verification_with_open_actions()
    {
        var nc = Raised();
        nc.Triage(Manager);
        nc.RecordRca(RcaMethod.Fishbone, "Analysis", Manager);
        nc.PlanCapaAction(CapaActionType.Corrective, "Fix", Owner, Due);

        var act = nc.SubmitForVerification;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("NC-020");
    }

    [Fact]
    public void Failed_verification_loops_back_to_action_plan()
    {
        var nc = Raised();
        nc.Triage(Manager);
        nc.RecordRca(RcaMethod.FiveWhys, "Analysis", Manager);
        var actionId = nc.PlanCapaAction(CapaActionType.Corrective, "Fix", Owner, Due);
        nc.CompleteCapaAction(actionId, Now);
        nc.SubmitForVerification();

        nc.Verify(passed: false, actorId: Manager);

        nc.Status.Should().Be(NcStatus.ActionPlan);
    }

    [Fact]
    public void Segregation_of_duties_raiser_cannot_verify_own_nc()
    {
        var nc = Raised();
        nc.Triage(Manager);
        nc.RecordRca(RcaMethod.FiveWhys, "Analysis", Manager);
        var actionId = nc.PlanCapaAction(CapaActionType.Corrective, "Fix", Owner, Due);
        nc.CompleteCapaAction(actionId, Now);
        nc.SubmitForVerification();

        var act = () => nc.Verify(passed: true, actorId: Raiser);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-CAPA-002");
        nc.Status.Should().Be(NcStatus.PendingVerification, "the illegal verify must not change state");
    }

    [Fact]
    public void Segregation_of_duties_raiser_cannot_close_own_nc()
    {
        var nc = Raised();
        nc.Triage(Manager);
        nc.RecordRca(RcaMethod.FiveWhys, "Analysis", Manager);
        var actionId = nc.PlanCapaAction(CapaActionType.Corrective, "Fix", Owner, Due);
        nc.CompleteCapaAction(actionId, Now);
        nc.SubmitForVerification();
        nc.Verify(passed: true, actorId: Manager);

        var act = () => nc.ConfirmEffectiveness(effective: true, actorId: Raiser);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-CAPA-001");
        nc.Status.Should().Be(NcStatus.EffectivenessCheck, "the illegal close must not change state");
    }

    [Fact]
    public void Reject_only_from_raised_and_requires_reason()
    {
        var nc = Raised();

        var noReason = () => nc.Reject(" ");
        noReason.Should().Throw<DomainException>().Which.Code.Should().Be("NC-013");

        nc.Reject("Duplicate of NC-2026-0000");
        nc.Status.Should().Be(NcStatus.Rejected);

        var again = () => nc.Reject("x");
        again.Should().Throw<InvalidStateTransitionException>();
    }
}
