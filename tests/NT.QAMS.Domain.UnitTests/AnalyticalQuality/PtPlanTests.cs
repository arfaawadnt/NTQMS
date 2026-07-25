using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class PtPlanTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Approval_freezes_the_lines_and_requires_at_least_one()
    {
        var plan = PtPlan.Create("PTP-2026-0001", 2026);

        var empty = () => plan.Approve(Qm, Now);
        empty.Should().Throw<DomainException>().Which.Code.Should().Be("PTP-011");

        plan.AddItem("EQAS Chemistry", "Glucose", "Bio-Rad", 2, null);
        plan.Approve(Qm, Now);
        plan.Status.Should().Be(PtPlanStatus.Approved);

        var lateEdit = () => plan.AddItem("EQAS Chemistry", "Sodium", null, 2, null);
        lateEdit.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("PTP-015");
    }

    [Fact]
    public void Fulfilment_counts_against_the_approved_plan()
    {
        var plan = PtPlan.Create("PTP-2026-0001", 2026);
        var itemId = plan.AddItem("EQAS Chemistry", "Glucose", "Bio-Rad", 2, null);

        var beforeApproval = () => plan.RecordFulfilment(itemId, "PT-2026-0007");
        beforeApproval.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("PTP-012");

        plan.Approve(Qm, Now);
        plan.RecordFulfilment(itemId, "PT-2026-0007");
        plan.RecordFulfilment(itemId, "PT-2026-0011");

        var item = plan.Items.Single();
        item.FulfilledCycles.Should().Be(2);
        item.LastEnrollmentRef.Should().Be("PT-2026-0011");
    }

    [Fact]
    public void Closure_demands_a_coverage_summary_and_only_from_approved()
    {
        var plan = PtPlan.Create("PTP-2026-0001", 2026);
        plan.AddItem("EQAS Chemistry", "Glucose", null, 2, null);

        var early = () => plan.Close("done");
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("PTP-013");

        plan.Approve(Qm, Now);
        var blank = () => plan.Close(" ");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be("PTP-014");

        plan.Close("1/2 cycles fulfilled; Q4 cycle missed — provider cancelled, gap carried to 2027 plan.");
        plan.Status.Should().Be(PtPlanStatus.Closed);
    }
}
