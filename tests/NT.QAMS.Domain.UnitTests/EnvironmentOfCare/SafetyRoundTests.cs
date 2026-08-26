using FluentAssertions;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.EnvironmentOfCare;

public class SafetyRoundTests
{
    private static readonly Guid Conductor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 9, 1);

    private static SafetyRound Scheduled() => SafetyRound.Schedule("EOR-1", "ICU wing", RoundType.FireSafety, Date);

    [Fact]
    public void Findings_can_only_be_added_while_in_progress()
    {
        var r = Scheduled();
        var early = () => r.AddFinding("Blocked fire exit", FindingSeverity.Critical);
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("EOC-011");

        r.Start(Conductor);
        r.AddFinding("Blocked fire exit", FindingSeverity.Critical);
        r.OpenFindingCount.Should().Be(1);
    }

    [Fact]
    public void Resolving_a_finding_requires_a_note_and_closes_it()
    {
        var r = Scheduled();
        r.Start(Conductor);
        var fid = r.AddFinding("Expired extinguisher", FindingSeverity.High);

        var noNote = () => r.ResolveFinding(fid, " ", Now);
        noNote.Should().Throw<DomainException>().Which.Code.Should().Be("EOC-013");

        r.ResolveFinding(fid, "Extinguisher replaced.", Now);
        r.OpenFindingCount.Should().Be(0);
        r.Findings.Single().Status.Should().Be(FindingStatus.Resolved);
    }

    [Fact]
    public void Resolving_an_already_resolved_finding_is_rejected()
    {
        var r = Scheduled();
        r.Start(Conductor);
        var fid = r.AddFinding("Loose cable", FindingSeverity.Low);
        r.ResolveFinding(fid, "Secured.", Now);
        var again = () => r.ResolveFinding(fid, "Again.", Now);
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("EOC-015");
    }

    [Fact]
    public void Only_an_in_progress_round_can_be_completed()
    {
        var r = Scheduled();
        var early = r.Complete;
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("EOC-016");

        r.Start(Conductor);
        r.Complete();
        r.Status.Should().Be(RoundStatus.Completed);
    }

    [Fact]
    public void An_area_is_required()
    {
        var act = () => SafetyRound.Schedule("EOR-X", " ", RoundType.Security, Date);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("EOC-001");
    }
}
