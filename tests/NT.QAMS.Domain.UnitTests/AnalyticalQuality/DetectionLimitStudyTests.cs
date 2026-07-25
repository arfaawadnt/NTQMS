using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class DetectionLimitStudyTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static DetectionLimitStudy New(decimal cvTarget = 20m) => DetectionLimitStudy.Configure(
        "DL-2026-0001", "Troponin I", "ng/L", "hs-cTnI (Architect)", cvTarget);

    /// <summary>Ten blanks alternating 0.15/0.25: mean 0.2, sample SD √(0.025/9) ≈ 0.0527.</summary>
    private static void AddBlanks(DetectionLimitStudy study)
    {
        for (var i = 0; i < 5; i++)
        {
            study.AddMeasurement(DetectionSampleKind.Blank, null, 0.15m);
            study.AddMeasurement(DetectionSampleKind.Blank, null, 0.25m);
        }
    }

    [Fact]
    public void Lob_lod_follow_the_ep17_parametric_formulas()
    {
        var study = New();
        AddBlanks(study);
        // Two low-level samples, 5 replicates each, alternating ±0.1 around their means
        // → each group's SD = √(0.05/4) ≈ 0.1118; pooled over 8 df identical.
        foreach (var (assigned, center) in new[] { (2.0m, 2.1m), (4.0m, 4.05m) })
        {
            study.AddMeasurement(DetectionSampleKind.LowLevel, assigned, center + 0.1m);
            study.AddMeasurement(DetectionSampleKind.LowLevel, assigned, center - 0.1m);
            study.AddMeasurement(DetectionSampleKind.LowLevel, assigned, center + 0.1m);
            study.AddMeasurement(DetectionSampleKind.LowLevel, assigned, center - 0.1m);
            study.AddMeasurement(DetectionSampleKind.LowLevel, assigned, center);
        }

        study.Calculate();

        study.BlankMean.Should().Be(0.2m);
        study.BlankSd.Should().BeApproximately(0.0527m, 0.0002m);
        // LoB = 0.2 + 1.645·0.0527 ≈ 0.2867
        study.Lob.Should().BeApproximately(0.2867m, 0.0005m);
        // Pooled SD: each group ss = 4·0.01 = 0.04, df 4 → total ss 0.08 / df 8 = 0.01 → SD 0.1
        study.PooledLowSd.Should().Be(0.1m);
        // LoD = LoB + 1.645·0.1 ≈ 0.4512
        study.Lod.Should().BeApproximately(0.4512m, 0.0005m);
    }

    [Fact]
    public void Loq_is_the_lowest_level_meeting_the_cv_goal_at_or_above_the_lod()
    {
        var study = New(cvTarget: 10m);
        AddBlanks(study);
        // Level 1.0: noisy (SD ~0.316 → CV ~31%) — fails the 10% goal.
        foreach (var v in new[] { 0.7m, 1.3m, 0.7m, 1.3m, 1.0m })
        {
            study.AddMeasurement(DetectionSampleKind.LowLevel, 1.0m, v);
        }

        // Level 3.0: tight (SD ~0.0707 → CV ~2.4%) — qualifies.
        foreach (var v in new[] { 2.95m, 3.05m, 2.95m, 3.05m, 3.0m })
        {
            study.AddMeasurement(DetectionSampleKind.LowLevel, 3.0m, v);
        }

        study.Calculate();

        var levels = study.LowLevelAssessments();
        levels.Should().HaveCount(2);
        levels[0].QualifiesForLoq.Should().BeFalse("31% CV misses the 10% goal");
        levels[1].QualifiesForLoq.Should().BeTrue();
        study.Loq.Should().Be(3.0m);
    }

    [Fact]
    public void Loq_stays_unestablished_when_no_level_qualifies()
    {
        var study = New(cvTarget: 5m);
        AddBlanks(study);
        foreach (var v in new[] { 0.7m, 1.3m, 0.7m, 1.3m, 1.0m, 0.6m, 1.4m, 0.6m, 1.4m, 1.0m })
        {
            study.AddMeasurement(DetectionSampleKind.LowLevel, 1.0m, v);
        }

        study.Calculate();
        study.Lod.Should().NotBeNull();
        study.Loq.Should().BeNull("no level met the 5% CV goal — claiming one would be dishonest");
    }

    [Fact]
    public void Guards_kinds_minimums_and_sign_off_freeze()
    {
        var study = New();
        var blankWithAssigned = () => study.AddMeasurement(DetectionSampleKind.Blank, 1m, 0.2m);
        blankWithAssigned.Should().Throw<DomainException>().Which.Code.Should().Be("DL-004");
        var lowWithoutAssigned = () => study.AddMeasurement(DetectionSampleKind.LowLevel, null, 1m);
        lowWithoutAssigned.Should().Throw<DomainException>().Which.Code.Should().Be("DL-003");

        study.AddMeasurement(DetectionSampleKind.Blank, null, 0.2m);
        var tooFew = () => study.Calculate();
        tooFew.Should().Throw<DomainException>().Which.Code.Should().Be("DL-010");

        AddBlanks(study); // Now 11 blanks.
        for (var i = 0; i < 5; i++)
        {
            study.AddMeasurement(DetectionSampleKind.LowLevel, 2m, 2m + (i % 2 == 0 ? 0.1m : -0.1m));
            study.AddMeasurement(DetectionSampleKind.LowLevel, 4m, 4m + (i % 2 == 0 ? 0.1m : -0.1m));
        }

        study.Calculate();
        study.SignOff(Qm, Now);
        study.DomainEvents.Should().ContainSingle(e => e is DetectionLimitSignedOff);

        var mutate = () => study.AddMeasurement(DetectionSampleKind.Blank, null, 0.2m);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("DL-014");
    }
}
