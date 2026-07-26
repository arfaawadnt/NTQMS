using FluentAssertions;
using NT.QAMS.Domain.Records;
using NT.QAMS.Domain.Sla;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Operations;

public class ArchiveEntryTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly Guid Snapshot = Guid.CreateVersion7();
    private static readonly DateOnly Archived = new(2020, 1, 15);

    private static ArchiveEntry FiveYear() =>
        ArchiveEntry.Archive("ARC-2020-0001", "DOCUMENTS", "SOP-CAL-045", Snapshot, RetentionClass.FiveYears, Archived, Actor);

    [Fact]
    public void Retention_expiry_is_derived_from_class()
    {
        FiveYear().RetentionExpiry.Should().Be(new DateOnly(2025, 1, 15));

        var permanent = ArchiveEntry.Archive(
            "ARC-1", "NC", "NC-1", Snapshot, RetentionClass.Permanent, Archived, Actor);
        permanent.RetentionExpiry.Should().BeNull();
    }

    [Fact]
    public void Disposal_blocked_before_expiry()
    {
        var entry = FiveYear();
        var early = () => entry.AuthorizeDisposal(Actor, new DateOnly(2024, 1, 1));
        early.Should().Throw<DomainException>().Which.Code.Should().Be("ARC-014");
    }

    [Fact]
    public void Disposal_allowed_after_expiry_and_raises_event()
    {
        var entry = FiveYear();
        entry.AuthorizeDisposal(Actor, new DateOnly(2025, 2, 1));

        entry.State.Should().Be(ArchiveState.Disposed);
        entry.DisposalAuthorizedBy.Should().Be(Actor);
        entry.DomainEvents.OfType<RecordDisposed>().Should().ContainSingle();
    }

    [Fact]
    public void Permanent_records_can_never_be_disposed()
    {
        var permanent = ArchiveEntry.Archive(
            "ARC-1", "NC", "NC-1", Snapshot, RetentionClass.Permanent, Archived, Actor);
        var act = () => permanent.AuthorizeDisposal(Actor, new DateOnly(2099, 1, 1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ARC-013");
    }

    [Fact]
    public void Retrieve_and_return_round_trip()
    {
        var entry = FiveYear();
        entry.Retrieve();
        entry.State.Should().Be(ArchiveState.Retrieved);
        entry.Return();
        entry.State.Should().Be(ArchiveState.Archived);
    }

    [Fact]
    public void Archiving_without_a_content_snapshot_is_rejected()
    {
        // F-14: an archive entry must carry an immutable content copy.
        var act = () => ArchiveEntry.Archive(
            "ARC-2", "DOCUMENTS", "SOP-1", Guid.Empty, RetentionClass.FiveYears, Archived, Actor);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ARC-002");
    }

    [Fact]
    public void Legal_hold_blocks_disposal_even_after_expiry()
    {
        // F-14: litigation/investigation hold overrides retention expiry.
        var entry = FiveYear();
        entry.PlaceLegalHold("Litigation ref 2025-88", Actor);
        entry.IsOnLegalHold.Should().BeTrue();
        entry.DomainEvents.OfType<ArchiveLegalHoldPlaced>().Should().ContainSingle();

        var disposeUnderHold = () => entry.AuthorizeDisposal(Actor, new DateOnly(2025, 2, 1));
        disposeUnderHold.Should().Throw<DomainException>().Which.Code.Should().Be("ARC-015");
    }

    [Fact]
    public void Releasing_a_legal_hold_restores_disposability()
    {
        var entry = FiveYear();
        entry.PlaceLegalHold("Litigation ref 2025-88", Actor);
        entry.ReleaseLegalHold(Actor);

        entry.IsOnLegalHold.Should().BeFalse();
        entry.DomainEvents.OfType<ArchiveLegalHoldReleased>().Should().ContainSingle();

        entry.AuthorizeDisposal(Actor, new DateOnly(2025, 2, 1));
        entry.State.Should().Be(ArchiveState.Disposed);
    }

    [Fact]
    public void A_legal_hold_requires_a_reason()
    {
        var entry = FiveYear();
        var act = () => entry.PlaceLegalHold(" ", Actor);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("ARC-030");
    }
}

public class EscalationTimerTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly DateTimeOffset Deadline = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ladder_advances_owner_then_qm_then_terminal()
    {
        var timer = EscalationTimer.Arm("CAPA:abc", Owner, Deadline);
        timer.Level.Should().Be(0);

        // Not yet due at +12h.
        timer.AdvanceIfDue(Deadline.AddHours(12));
        timer.Level.Should().Be(0);

        // Level 1 at +24h → owner.
        timer.AdvanceIfDue(Deadline.AddHours(25));
        timer.Level.Should().Be(1);
        var l1 = timer.DomainEvents.OfType<EscalationTriggered>().Last();
        l1.AssigneeUserId.Should().Be(Owner);
        l1.RecipientRole.Should().BeNull();

        // Level 2 at +48h → QM role.
        timer.AdvanceIfDue(Deadline.AddHours(49));
        timer.Level.Should().Be(2);
        timer.DomainEvents.OfType<EscalationTriggered>().Last().RecipientRole.Should().Be("QualityManager");

        // Level 3 at +72h → terminal.
        timer.AdvanceIfDue(Deadline.AddHours(73));
        timer.Level.Should().Be(3);
        timer.NextStepAtUtc.Should().BeNull();

        // No further advancement.
        timer.AdvanceIfDue(Deadline.AddHours(200));
        timer.Level.Should().Be(3);
        timer.DomainEvents.OfType<EscalationTriggered>().Should().HaveCount(3);
    }

    [Fact]
    public void Cancelled_timer_does_not_advance()
    {
        var timer = EscalationTimer.Arm("CAPA:abc", Owner, Deadline);
        timer.Cancel();
        timer.AdvanceIfDue(Deadline.AddHours(100));
        timer.Level.Should().Be(0);
        timer.DomainEvents.Should().BeEmpty();
    }
}

public class WorkTaskTests
{
    [Fact]
    public void Task_requires_a_user_or_role_assignee()
    {
        var act = () => WorkTask.Create("Do thing", null, null, null, new DateOnly(2026, 8, 1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("TASK-002");
    }

    [Fact]
    public void Completion_is_idempotent_guarded()
    {
        var task = WorkTask.Create("Review CAPA", "CAPA:1", Guid.CreateVersion7(), null, new DateOnly(2026, 8, 1));
        task.Complete(new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero));
        task.Status.Should().Be(WorkTaskStatus.Completed);

        var again = () => task.Complete(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("TASK-003");
    }
}

public class SlaDefinitionTests
{
    [Fact]
    public void Target_hours_must_be_positive()
    {
        var act = () => SlaDefinition.Create("CAPA", "HIGH", 0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SLA-002");
    }
}
