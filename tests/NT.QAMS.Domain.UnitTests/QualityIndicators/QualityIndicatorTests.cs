using FluentAssertions;
using NT.QAMS.Domain.QualityIndicators;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.QualityIndicators;

public class QualityIndicatorTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static QualityIndicator HigherIsBetter() =>
        QualityIndicator.Define(
            "IND-2026-0001", "HH-1", "Hand hygiene compliance", null,
            "Compliant moments", "Observed moments", "%", 100m,
            IndicatorFrequency.Monthly, IndicatorDirection.HigherIsBetter);

    private static QualityIndicator LowerIsBetter() =>
        QualityIndicator.Define(
            "IND-2026-0002", "FALL-1", "Falls per 1,000 patient-days", null,
            "Falls", "Patient-days", "per 1,000 patient-days", 1000m,
            IndicatorFrequency.Monthly, IndicatorDirection.LowerIsBetter);

    private static DateOnly Period(int month) => new(2026, month, 1);

    [Fact]
    public void Records_a_measurement_and_computes_the_rate()
    {
        var indicator = HigherIsBetter();
        indicator.RecordMeasurement(Period(1), 90m, 100m, Actor, Now);

        indicator.Measurements.Should().ContainSingle()
            .Which.Value.Should().Be(90m, "90/100 × 100 = 90%");
    }

    [Fact]
    public void Denominator_must_be_positive()
    {
        var act = () => HigherIsBetter().RecordMeasurement(Period(1), 5m, 0m, Actor, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("IND-014");
    }

    [Fact]
    public void One_measurement_per_period()
    {
        var indicator = HigherIsBetter();
        indicator.RecordMeasurement(Period(1), 90m, 100m, Actor, Now);

        var act = () => indicator.RecordMeasurement(Period(1), 80m, 100m, Actor, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("IND-016");
    }

    [Fact]
    public void Higher_is_better_grades_below_action_as_breached_and_raises_the_event()
    {
        var indicator = HigherIsBetter();
        indicator.SetTargets(target: 90m, warningThreshold: 80m, actionThreshold: 70m);

        indicator.RecordMeasurement(Period(1), 65m, 100m, Actor, Now); // 65% ≤ 70 action

        indicator.Measurements.Single().Status.Should().Be(MeasurementStatus.Breached);
        indicator.DomainEvents.OfType<IndicatorBreached>().Should().ContainSingle()
            .Which.Value.Should().Be(65m);
    }

    [Fact]
    public void Higher_is_better_grades_between_warning_and_action_as_warning()
    {
        var indicator = HigherIsBetter();
        indicator.SetTargets(90m, 80m, 70m);

        indicator.RecordMeasurement(Period(1), 75m, 100m, Actor, Now); // 75% ≤ 80 warning, > 70 action

        indicator.Measurements.Single().Status.Should().Be(MeasurementStatus.Warning);
        indicator.DomainEvents.OfType<IndicatorBreached>().Should().BeEmpty();
    }

    [Fact]
    public void Lower_is_better_grades_above_action_as_breached()
    {
        var indicator = LowerIsBetter();
        indicator.SetTargets(target: 2m, warningThreshold: 4m, actionThreshold: 6m);

        indicator.RecordMeasurement(Period(1), 7m, 1000m, Actor, Now); // 7 per 1,000 ≥ 6 action

        indicator.Measurements.Single().Status.Should().Be(MeasurementStatus.Breached);
        indicator.DomainEvents.OfType<IndicatorBreached>().Should().ContainSingle();
    }

    [Fact]
    public void Threshold_consistency_is_enforced_per_direction()
    {
        // Higher-is-better: the action floor must be at or below the warning level.
        var act = () => HigherIsBetter().SetTargets(90m, 70m, 80m);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("IND-012");
    }

    [Fact]
    public void A_retired_indicator_accepts_no_new_measurements()
    {
        var indicator = HigherIsBetter();
        indicator.Retire();

        var act = () => indicator.RecordMeasurement(Period(1), 90m, 100m, Actor, Now);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("IND-013");
    }

    [Fact]
    public void Define_requires_numerator_and_denominator_definitions()
    {
        var act = () => QualityIndicator.Define(
            "IND-1", "C", "N", null, " ", "D", "%", 100m,
            IndicatorFrequency.Monthly, IndicatorDirection.HigherIsBetter);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("IND-003");
    }
}
