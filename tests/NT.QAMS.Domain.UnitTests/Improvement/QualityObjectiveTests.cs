using FluentAssertions;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Improvement;

public sealed class QualityObjectiveTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Recorder = Guid.CreateVersion7();

    private static QualityObjective NcClosureObjective() => QualityObjective.Define(
        "QO-2026-0001", "Close NCs promptly", null, "% of NCs closed within 30 days", "%",
        targetValue: 90m, ObjectiveDirection.AtLeast, Owner,
        new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    [Fact]
    public void Latest_measurement_drives_the_on_target_verdict()
    {
        var objective = NcClosureObjective();
        objective.OnTarget.Should().BeNull("nothing measured yet");

        objective.RecordProgress(new DateOnly(2026, 3, 31), 82m, Recorder, "Q1");
        objective.OnTarget.Should().BeFalse();

        objective.RecordProgress(new DateOnly(2026, 6, 30), 93m, Recorder, "Q2");
        objective.CurrentValue.Should().Be(93m);
        objective.OnTarget.Should().BeTrue();
    }

    [Fact]
    public void AtMost_direction_inverts_the_verdict()
    {
        var objective = QualityObjective.Define(
            "QO-2026-0002", "Contain complaint volume", null, "Complaints per 1000 reports", "count",
            targetValue: 2m, ObjectiveDirection.AtMost, Owner,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        objective.RecordProgress(new DateOnly(2026, 6, 30), 1.4m, Recorder, null);
        objective.OnTarget.Should().BeTrue();
        objective.RecordProgress(new DateOnly(2026, 9, 30), 2.6m, Recorder, null);
        objective.OnTarget.Should().BeFalse();
    }

    [Fact]
    public void Achieved_closure_is_refused_against_the_evidence()
    {
        var objective = NcClosureObjective();
        objective.RecordProgress(new DateOnly(2026, 12, 31), 84m, Recorder, "Year end");

        var dishonest = () => objective.CloseAsAchieved("We did great");
        dishonest.Should().Throw<DomainException>().Which.Code.Should().Be("OBJ-011");

        objective.CloseAsMissed("84% vs 90% target — capacity gap in Q3, corrective action CR-042 raised.");
        objective.Status.Should().Be(ObjectiveStatus.Missed);

        var late = () => objective.RecordProgress(new DateOnly(2027, 1, 5), 95m, Recorder, null);
        late.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("OBJ-010");
    }
}
