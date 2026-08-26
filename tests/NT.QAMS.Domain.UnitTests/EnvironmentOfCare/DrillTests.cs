using FluentAssertions;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.EnvironmentOfCare;

public class DrillTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 9, 1);

    private static Drill Scheduled() => Drill.Schedule("EOD-1", DrillType.Fire, "Tower A", Date);

    [Fact]
    public void Scheduled_to_executed_to_evaluated_is_the_happy_path()
    {
        var d = Scheduled();
        d.Execute(Now, participantCount: 42);
        d.Status.Should().Be(DrillStatus.Executed);
        d.ParticipantCount.Should().Be(42);

        d.Evaluate(90, "Minor delay clearing ward B.");
        d.Status.Should().Be(DrillStatus.Evaluated);
        d.EvaluationScore.Should().Be(90);
        d.Effectiveness.Should().Be("Effective");
    }

    [Theory]
    [InlineData(90, "Effective")]
    [InlineData(70, "PartiallyEffective")]
    [InlineData(40, "Ineffective")]
    public void Effectiveness_tiers_from_the_score(int score, string tier)
    {
        var d = Scheduled();
        d.Execute(Now, 10);
        d.Evaluate(score, "notes");
        d.Effectiveness.Should().Be(tier);
    }

    [Fact]
    public void Cannot_evaluate_before_execution()
    {
        var d = Scheduled();
        var act = () => d.Evaluate(90, "x");
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("DRL-012");
    }

    [Fact]
    public void The_evaluation_score_is_bounded()
    {
        var d = Scheduled();
        d.Execute(Now, 10);
        var act = () => d.Evaluate(150, "x");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("DRL-013");
    }

    [Fact]
    public void A_fresh_drill_has_no_effectiveness()
    {
        Scheduled().Effectiveness.Should().BeNull();
    }
}
