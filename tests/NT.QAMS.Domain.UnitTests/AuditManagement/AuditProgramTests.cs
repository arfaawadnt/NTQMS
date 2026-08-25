using FluentAssertions;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AuditManagement;

public class AuditProgramTests
{
    private static AuditProgram DraftWithLine(out Guid lineId)
    {
        var program = AuditProgram.Create(2026, "2026 Internal Audit Programme");
        lineId = program.AddPlannedAudit("Laboratory", null, "GAHAR-LAB", PlannedAuditPriority.High, 1);
        return program;
    }

    [Fact]
    public void Cannot_activate_an_empty_programme()
    {
        var program = AuditProgram.Create(2026, "Empty");
        var act = program.Activate;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("APG-014");
    }

    [Fact]
    public void Planned_quarter_must_be_1_to_4()
    {
        var program = AuditProgram.Create(2026, "P");
        var act = () => program.AddPlannedAudit("Pharmacy", null, null, PlannedAuditPriority.Medium, 5);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("APG-012");
    }

    [Fact]
    public void Full_cycle_schedule_then_complete()
    {
        var program = DraftWithLine(out var lineId);
        program.Activate();
        var auditId = Guid.CreateVersion7();

        program.LinkScheduledAudit(lineId, auditId);
        program.Plan.Single().Status.Should().Be(PlannedAuditStatus.Scheduled);
        program.Plan.Single().ScheduledAuditId.Should().Be(auditId);

        program.CompletePlannedAudit(lineId, new DateOnly(2026, 3, 15));
        program.Plan.Single().Status.Should().Be(PlannedAuditStatus.Completed);
        program.Plan.Single().CompletedOn.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void Cannot_complete_a_line_that_was_not_scheduled()
    {
        var program = DraftWithLine(out var lineId);
        program.Activate();

        var act = () => program.CompletePlannedAudit(lineId, new DateOnly(2026, 3, 15));
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("APG-021");
    }

    [Fact]
    public void Cannot_schedule_before_activation()
    {
        var program = DraftWithLine(out var lineId);
        var act = () => program.LinkScheduledAudit(lineId, Guid.CreateVersion7());
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("APG-015");
    }

    [Fact]
    public void Closed_programme_takes_no_new_lines()
    {
        var program = DraftWithLine(out _);
        program.Activate();
        program.Close();

        var act = () => program.AddPlannedAudit("Radiology", null, null, PlannedAuditPriority.Low, 2);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("APG-010");
    }

    [Fact]
    public void Activation_raises_the_event_with_the_planned_count()
    {
        var program = DraftWithLine(out _);
        program.AddPlannedAudit("Pharmacy", null, null, PlannedAuditPriority.Medium, 2);

        program.Activate();

        program.DomainEvents.OfType<AuditProgramActivated>().Should().ContainSingle()
            .Which.PlannedCount.Should().Be(2);
    }
}
