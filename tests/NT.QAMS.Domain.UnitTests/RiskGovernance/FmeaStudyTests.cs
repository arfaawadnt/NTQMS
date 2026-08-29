using FluentAssertions;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.RiskGovernance;

public class FmeaStudyTests
{
    private static FmeaStudy DraftWithMode(out Guid modeId, int s = 8, int o = 6, int d = 5)
    {
        var fmea = FmeaStudy.Create("FMEA-2026-0001", "Medication administration", "Med admin", FmeaType.Hfmea);
        modeId = fmea.AddFailureMode("Dispensing", "Wrong drug selected", "Patient harm", "Look-alike packaging", s, o, d);
        return fmea;
    }

    [Fact]
    public void Rpn_is_severity_times_occurrence_times_detection()
    {
        var fmea = DraftWithMode(out _, 8, 6, 5);
        fmea.FailureModes.Single().Rpn.Should().Be(240);
    }

    [Fact]
    public void Ratings_must_be_1_to_10()
    {
        var fmea = FmeaStudy.Create("F", "T", "P", FmeaType.Fmea);
        var act = () => fmea.AddFailureMode("Step", "Mode", "e", "c", 11, 5, 5);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("FME-019");
    }

    [Fact]
    public void Cannot_activate_without_a_failure_mode()
    {
        var fmea = FmeaStudy.Create("F", "T", "P", FmeaType.Fmea);
        var act = fmea.Activate;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("FME-016");
    }

    [Fact]
    public void Residual_re_score_lowers_the_rpn_and_marks_actioned()
    {
        var fmea = DraftWithMode(out var modeId, 8, 6, 5); // RPN 240
        fmea.Activate();
        fmea.RecommendAction(modeId, "Barcode scanning at dispensing", null);
        fmea.RecordResidual(modeId, 8, 2, 2); // occurrence + detection improved → 32

        var mode = fmea.FailureModes.Single();
        mode.ResidualRpn.Should().Be(32);
        mode.Status.Should().Be(FailureModeStatus.Actioned);
    }

    [Fact]
    public void A_closed_fmea_is_immutable()
    {
        var fmea = DraftWithMode(out _);
        fmea.Activate();
        fmea.Close();

        var act = () => fmea.AddFailureMode("Step", "Mode", "e", "c", 5, 5, 5);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("FME-010");
    }

    [Fact]
    public void Activation_raises_the_event_with_the_mode_count()
    {
        var fmea = DraftWithMode(out _);
        fmea.Activate();
        fmea.DomainEvents.OfType<FmeaActivated>().Should().ContainSingle()
            .Which.FailureModeCount.Should().Be(1);
    }

    [Fact]
    public void Cannot_record_residual_before_a_recommended_action()
    {
        // M-22: scoring residual risk marks the mode Actioned — that must not be
        // possible with no recommended action on record.
        var fmea = DraftWithMode(out var modeId, 8, 6, 5);
        fmea.Activate();

        var act = () => fmea.RecordResidual(modeId, 8, 2, 2);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("FME-020");
        fmea.FailureModes.Single().Status.Should().Be(FailureModeStatus.Open);
    }
}
