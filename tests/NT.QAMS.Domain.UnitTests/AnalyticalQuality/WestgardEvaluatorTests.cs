using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

/// <summary>
/// Exhaustive Westgard rule coverage. Profile: mean 100, SD 5, so
/// z=+1 is 105, z=+2 is 110, z=+3 is 115, z=-2 is 90, z=-3 is 85.
/// </summary>
public class WestgardEvaluatorTests
{
    private const decimal Mean = 100m;
    private const decimal Sd = 5m;

    private static WestgardVerdict Evaluate(decimal value, params decimal[] prior) =>
        WestgardEvaluator.Evaluate(value, Mean, Sd, prior);

    [Fact]
    public void In_control_value_passes()
    {
        var v = Evaluate(103m); // z = 0.6
        v.Outcome.Should().Be(WestgardOutcome.InControl);
        v.ViolatedRules.Should().BeEmpty();
    }

    [Fact]
    public void Rule_1_2s_is_warning_only()
    {
        var v = Evaluate(111m); // z = 2.2
        v.Outcome.Should().Be(WestgardOutcome.Warning);
        v.ViolatedRules.Should().ContainSingle().Which.Should().Be("1-2s");
    }

    [Fact]
    public void Rule_1_3s_rejects()
    {
        var v = Evaluate(116m); // z = 3.2
        v.Outcome.Should().Be(WestgardOutcome.OutOfControl);
        v.ViolatedRules.Should().Contain("1-3s");
    }

    [Fact]
    public void Rule_2_2s_rejects_two_consecutive_same_side_beyond_2sd()
    {
        var v = Evaluate(111m, 112m); // both z > +2, same side
        v.Outcome.Should().Be(WestgardOutcome.OutOfControl);
        v.ViolatedRules.Should().Contain("2-2s");
    }

    [Fact]
    public void Rule_2_2s_does_not_fire_for_opposite_sides()
    {
        var v = Evaluate(111m, 89m); // z=+2.2 now, z=-2.2 prior — opposite sides
        v.ViolatedRules.Should().NotContain("2-2s");
    }

    [Fact]
    public void Rule_R_4s_rejects_span_over_4sd()
    {
        var v = Evaluate(112m, 88m); // z=+2.4 and z=-2.4 → span 4.8 SD
        v.Outcome.Should().Be(WestgardOutcome.OutOfControl);
        v.ViolatedRules.Should().Contain("R-4s");
    }

    [Fact]
    public void Rule_10x_rejects_ten_consecutive_same_side()
    {
        // 9 prior values all just above the mean, plus a 10th above → 10-x.
        var prior = new decimal[] { 101, 102, 101, 103, 102, 101, 104, 102, 101 };
        var v = Evaluate(102m, prior);
        v.Outcome.Should().Be(WestgardOutcome.OutOfControl);
        v.ViolatedRules.Should().Contain("10-x");
    }

    [Fact]
    public void Rule_10x_does_not_fire_when_a_value_crosses_the_mean()
    {
        var prior = new decimal[] { 101, 102, 101, 99, 102, 101, 104, 102, 101 }; // one below mean
        var v = Evaluate(102m, prior);
        v.ViolatedRules.Should().NotContain("10-x");
    }

    [Fact]
    public void Zero_sd_is_rejected()
    {
        var act = () => WestgardEvaluator.Evaluate(100m, 100m, 0m, []);
        act.Should().Throw<SharedKernel.Primitives.DomainException>().Which.Code.Should().Be("QC-SD");
    }
}

public class ValidationStudyTests
{
    private static readonly Guid Signer = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static ValidationStudy Configured() =>
        ValidationStudy.Configure("MV-2026-0001", "Glucose", "EP05", totalAllowableError: 10m);

    [Fact]
    public void Statistics_require_at_least_two_replicates()
    {
        var study = Configured();
        study.EnterReplicate("L2", 100m, 100m);

        var act = study.CalculateStatistics;
        act.Should().Throw<SharedKernel.Primitives.DomainException>().Which.Code.Should().Be("MV-013");
    }

    [Fact]
    public void Tight_precision_passes_TEa()
    {
        var study = Configured();
        foreach (var m in new decimal[] { 100, 101, 99, 100, 101 })
        {
            study.EnterReplicate("L2", m, 100m);
        }

        study.CalculateStatistics();

        study.State.Should().Be(ValidationState.StatsCalculated);
        study.Cv.Should().BeLessThan(1m);
        study.Passed.Should().BeTrue();
    }

    [Fact]
    public void Wide_scatter_fails_TEa()
    {
        var study = ValidationStudy.Configure("MV-2026-0002", "Glucose", "EP05", totalAllowableError: 5m);
        foreach (var m in new decimal[] { 100, 120, 80, 115, 90 })
        {
            study.EnterReplicate("L2", m, 100m);
        }

        study.CalculateStatistics();
        study.Passed.Should().BeFalse();
    }

    [Fact]
    public void Sign_off_locks_the_study()
    {
        var study = Configured();
        study.EnterReplicate("L2", 100m, 100m);
        study.EnterReplicate("L2", 101m, 100m);
        study.CalculateStatistics();
        study.SignOff(Signer, Now);

        study.State.Should().Be(ValidationState.SignedOff);
        study.DomainEvents.OfType<ValidationStudySignedOff>().Should().ContainSingle();

        var addMore = () => study.EnterReplicate("L2", 102m, 100m);
        addMore.Should().Throw<SharedKernel.Primitives.InvalidStateTransitionException>()
            .Which.Code.Should().Be("MV-010");
    }

    [Fact]
    public void Reopening_after_calculation_voids_prior_results()
    {
        var study = Configured();
        study.EnterReplicate("L2", 100m, 100m);
        study.EnterReplicate("L2", 101m, 100m);
        study.CalculateStatistics();
        study.Cv.Should().NotBeNull();

        study.EnterReplicate("L2", 102m, 100m); // reopen
        study.State.Should().Be(ValidationState.DataEntered);
        study.Cv.Should().BeNull("prior results are voided until recalculated");
    }
}

public class PtEnrollmentTests
{
    private static readonly Guid Raiser = Guid.CreateVersion7();

    private static PtEnrollment Enrolled() => PtEnrollment.Enroll("PT-2026-0001", "CAP", "Glucose", "2026-C1");

    [Theory]
    [InlineData(100.0, 100.0, 5.0, "Satisfactory", 0.0)]   // z = 0
    [InlineData(112.0, 100.0, 5.0, "Questionable", 2.4)]   // 2 < |z| < 3
    [InlineData(116.0, 100.0, 5.0, "Unsatisfactory", 3.2)] // |z| >= 3
    [InlineData(84.0, 100.0, 5.0, "Unsatisfactory", -3.2)] // negative side
    public void Performance_category_follows_z_score(
        double submitted, double assigned, double sd, string expected, double expectedZ)
    {
        var pt = Enrolled();
        pt.RecordResult((decimal)submitted, (decimal)assigned, (decimal)sd, Raiser);

        pt.Performance.ToString().Should().Be(expected);
        pt.ZScore.Should().Be(Math.Round((decimal)expectedZ, 3));
    }

    [Fact]
    public void Unsatisfactory_raises_the_saga_event()
    {
        var pt = Enrolled();
        pt.RecordResult(116m, 100m, 5m, Raiser);

        pt.DomainEvents.OfType<PtUnsatisfactory>().Should().ContainSingle()
            .Which.ZScore.Should().Be(3.2m);
    }

    [Fact]
    public void Satisfactory_raises_nothing()
    {
        var pt = Enrolled();
        pt.RecordResult(101m, 100m, 5m, Raiser);
        pt.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Result_can_only_be_recorded_once()
    {
        var pt = Enrolled();
        pt.RecordResult(101m, 100m, 5m, Raiser);

        var again = () => pt.RecordResult(102m, 100m, 5m, Raiser);
        again.Should().Throw<SharedKernel.Primitives.InvalidStateTransitionException>()
            .Which.Code.Should().Be("PT-010");
    }
}
