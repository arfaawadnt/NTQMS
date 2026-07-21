using FluentAssertions;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AuditManagement;

public class AuditTests
{
    private static readonly Guid Lead = Guid.CreateVersion7();
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateOnly Planned = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static Audit InProgressAudit()
    {
        var audit = Audit.Schedule("AUD-2026-0001", "Q3 internal audit", AuditType.Internal, Lead, Planned);
        audit.AddChecklistItem("7.2", "Are methods validated before use?");
        audit.Start();
        return audit;
    }

    [Fact]
    public void Cannot_start_without_checklist()
    {
        var audit = Audit.Schedule("AUD-2026-0001", "Audit", AuditType.Internal, Lead, Planned);
        var act = audit.Start;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("AUD-011");
    }

    [Fact]
    public void Sign_off_blocked_while_checklist_unanswered()
    {
        var audit = InProgressAudit();
        var act = () => audit.SignOff(Qm, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("AUD-017");
    }

    [Fact]
    public void Nc_graded_finding_blocks_sign_off_until_acknowledged()
    {
        var audit = InProgressAudit();
        audit.AnswerChecklistItem(audit.Checklist[0].Id, ChecklistVerdict.NonConform, "Method X unvalidated");
        var findingId = audit.RaiseFinding(FindingGrade.MajorNc, "Method X in use without validation", Lead);

        var blocked = () => audit.SignOff(Qm, Now);
        blocked.Should().Throw<DomainException>().Which.Code.Should().Be("AUD-018");

        audit.AcknowledgeFindingNc(findingId, Guid.CreateVersion7());
        audit.SignOff(Qm, Now);

        audit.Status.Should().Be(AuditStatus.SignedOff);
        audit.DomainEvents.OfType<AuditSignedOff>().Should().ContainSingle();
    }

    [Fact]
    public void Ofi_findings_do_not_block_sign_off()
    {
        var audit = InProgressAudit();
        audit.AnswerChecklistItem(audit.Checklist[0].Id, ChecklistVerdict.Ofi, "Could improve");
        audit.RaiseFinding(FindingGrade.Ofi, "Consider automating the log", Lead);

        audit.SignOff(Qm, Now);
        audit.Status.Should().Be(AuditStatus.SignedOff);
    }

    [Fact]
    public void FindingRaised_event_carries_tenant_and_actor_for_the_saga()
    {
        var audit = InProgressAudit();
        audit.TenantId = Guid.CreateVersion7();

        audit.RaiseFinding(FindingGrade.MinorNc, "Records incomplete", Lead);

        var evt = audit.DomainEvents.OfType<FindingRaised>().Single();
        evt.TenantId.Should().Be(audit.TenantId);
        evt.RaisedBy.Should().Be(Lead);
        evt.AuditRef.Should().Be("AUD-2026-0001");
    }

    [Fact]
    public void Signed_off_audit_is_immutable()
    {
        var audit = InProgressAudit();
        audit.AnswerChecklistItem(audit.Checklist[0].Id, ChecklistVerdict.Conform, null);
        audit.SignOff(Qm, Now);

        var addItem = () => audit.AddChecklistItem("8.7", "New question");
        addItem.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("AUD-020");

        var finding = () => audit.RaiseFinding(FindingGrade.MinorNc, "Late finding", Lead);
        finding.Should().Throw<InvalidStateTransitionException>();
    }
}
