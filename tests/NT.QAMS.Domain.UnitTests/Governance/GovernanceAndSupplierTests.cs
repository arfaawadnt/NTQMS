using FluentAssertions;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Governance;

public class RiskItemTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly DateOnly Due = new(2026, 9, 1);

    private static RiskItem NewRisk() =>
        RiskItem.Assess("RSK-2026-0001", "Reagent supply disruption", "Operational", 4, 4);

    [Fact]
    public void Assessment_requires_explicit_scores_and_computes_rpn()
    {
        NewRisk().Rpn.Should().Be(16);

        var act = () => RiskItem.Assess("R", "T", "Op", 0, 3);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("RSK-002");
    }

    [Fact]
    public void High_residual_raises_alert_event()
    {
        var risk = NewRisk();
        risk.RecordResidualAssessment(4, 4); // 16 > 12

        risk.DomainEvents.OfType<HighResidualRisk>().Should().ContainSingle()
            .Which.ResidualRpn.Should().Be(16);
    }

    [Fact]
    public void Close_requires_residual_and_completed_actions()
    {
        var risk = NewRisk();
        var actionId = risk.AddMitigationAction("Second supplier qualified", Owner, Due);

        var noResidual = () => risk.Close();
        noResidual.Should().Throw<DomainException>().Which.Code.Should().Be("RSK-005");

        risk.RecordResidualAssessment(2, 2);
        var openAction = () => risk.Close();
        openAction.Should().Throw<DomainException>().Which.Code.Should().Be("RSK-006");

        risk.CompleteMitigationAction(actionId);
        risk.Close();

        risk.Status.Should().Be(RiskStatus.Closed);
        risk.DomainEvents.OfType<RiskClosed>().Should().ContainSingle();

        var mutate = () => risk.AddMitigationAction("late", Owner, Due);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("RSK-007");
    }
}

public class ChangeRequestTests
{
    private static readonly Guid Proposer = Guid.CreateVersion7();
    private static readonly Guid Approver = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Approval_requires_linked_risk_assessment()
    {
        var change = ChangeRequest.Propose("CHG-2026-0001", "New LIS interface", "Impacts result flow", Proposer);

        var withoutRisk = () => change.Approve(Approver, Now);
        withoutRisk.Should().Throw<DomainException>().Which.Code.Should().Be("CHG-012");

        change.LinkRiskAssessment(Guid.CreateVersion7());
        change.Approve(Approver, Now);

        change.Status.Should().Be(ChangeStatus.Approved);
        change.DomainEvents.OfType<ChangeApproved>().Should().ContainSingle();
    }

    [Fact]
    public void Closed_change_is_immutable()
    {
        var change = ChangeRequest.Propose("CHG-2026-0001", "T", "Impact", Proposer);
        change.LinkRiskAssessment(Guid.CreateVersion7());
        change.Approve(Approver, Now);
        change.Close("Deployed to production");

        var reopen = () => change.Approve(Approver, Now);
        reopen.Should().Throw<InvalidStateTransitionException>();
    }

    private static ChangeRequest Implemented()
    {
        var change = ChangeRequest.Propose("CHG-2026-0002", "T", "Impact", Proposer);
        change.LinkRiskAssessment(Guid.CreateVersion7());
        change.Approve(Approver, Now);
        change.Close("Deployed to production");
        return change;
    }

    [Fact]
    public void Post_implementation_review_verifies_effectiveness_and_is_terminal()
    {
        // F-11: the change lifecycle now has an effectiveness/verification stage.
        var change = Implemented();
        change.RecordPostImplementationReview(Approver, effective: true, "KPIs confirm the change met its objective.", Now);

        change.Status.Should().Be(ChangeStatus.Reviewed);
        change.ChangeEffective.Should().BeTrue();
        change.PostImplementationReviewedBy.Should().Be(Approver);
        change.DomainEvents.OfType<ChangePostImplementationReviewed>().Should().ContainSingle();

        var reReview = () => change.RecordPostImplementationReview(Approver, false, "again", Now);
        reReview.Should().Throw<DomainException>().Which.Code.Should().Be("CHG-020");
    }

    [Fact]
    public void Review_requires_notes_and_only_applies_to_a_closed_change()
    {
        Implemented().Invoking(c => c.RecordPostImplementationReview(Approver, true, " ", Now))
            .Should().Throw<DomainException>().Which.Code.Should().Be("CHG-021");

        var approvedNotClosed = ChangeRequest.Propose("CHG-3", "T", "Impact", Proposer);
        approvedNotClosed.LinkRiskAssessment(Guid.CreateVersion7());
        approvedNotClosed.Approve(Approver, Now);
        approvedNotClosed.Invoking(c => c.RecordPostImplementationReview(Approver, true, "x", Now))
            .Should().Throw<DomainException>().Which.Code.Should().Be("CHG-020");
    }
}

public class ManagementReviewTests
{
    private static readonly Guid Chair = Guid.CreateVersion7();

    [Fact]
    public void Closed_review_minutes_are_immutable()
    {
        var review = ManagementReview.Schedule("MRV-2026-01", "Q2 review", new DateOnly(2026, 7, 1), "QM, Director");
        review.AddDecision("Hire second technician", Chair, new DateOnly(2026, 9, 1));

        var noMinutes = () => review.Close(Chair, " ");
        noMinutes.Should().Throw<DomainException>().Which.Code.Should().Be("MRV-003");

        review.Close(Chair, "Reviewed KPIs; two decisions recorded.");
        review.Status.Should().Be(ReviewStatus.Closed);

        var lateDecision = () => review.AddDecision("Late", Chair, new DateOnly(2026, 10, 1));
        lateDecision.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MRV-004");
    }
}

public class SupplierTests
{
    private static readonly Guid Registrant = Guid.CreateVersion7();
    private static readonly Guid Approver = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 7, 22);

    private static Supplier NewSupplier() =>
        Supplier.Register("SUP-2026-0001", "Acme Reagents", "Reagents", Registrant);

    [Fact]
    public void Registrant_cannot_approve_own_supplier()
    {
        var supplier = NewSupplier();

        var self = () => supplier.Approve(Registrant);
        self.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-SUP-001");

        supplier.Approve(Approver);
        supplier.Status.Should().Be(SupplierStatus.Approved);
    }

    [Fact]
    public void Expired_certificate_sweep_proposal_suspends_approved_supplier()
    {
        var supplier = NewSupplier();
        supplier.AddCertificate("ISO 9001", Today.AddDays(10), null);
        supplier.Approve(Approver);

        supplier.SuspendIfCertificateExpired(Today); // still valid — declined
        supplier.Status.Should().Be(SupplierStatus.Approved);

        supplier.SuspendIfCertificateExpired(Today.AddDays(11));
        supplier.Status.Should().Be(SupplierStatus.Suspended);
        supplier.SuspensionReason.Should().Contain("ISO 9001");
        supplier.DomainEvents.OfType<SupplierSuspended>().Should().ContainSingle();

        supplier.SuspendIfCertificateExpired(Today.AddDays(12)); // idempotent — already suspended
        supplier.DomainEvents.OfType<SupplierSuspended>().Should().ContainSingle();
    }

    [Fact]
    public void Evaluation_computes_weighted_total_and_validates_inputs()
    {
        var evaluation = SupplierEvaluation.Record(
            Guid.CreateVersion7(), new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30),
            [("Delivery", 2m, 90m), ("Quality", 3m, 80m)], Approver);

        evaluation.WeightedTotal.Should().Be(84.00m); // (2*90 + 3*80) / 5

        var badPeriod = () => SupplierEvaluation.Record(
            Guid.CreateVersion7(), new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1),
            [("Delivery", 1m, 90m)], Approver);
        badPeriod.Should().Throw<DomainException>().Which.Code.Should().Be("SUP-020");

        var badScore = () => SupplierEvaluation.Record(
            Guid.CreateVersion7(), new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30),
            [("Delivery", 1m, 101m)], Approver);
        badScore.Should().Throw<DomainException>().Which.Code.Should().Be("SUP-023");
    }
}
