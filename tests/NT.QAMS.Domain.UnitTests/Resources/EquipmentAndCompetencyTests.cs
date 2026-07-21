using FluentAssertions;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Resources;

public class EquipmentItemTests
{
    private static readonly DateOnly Today = new(2026, 7, 22);

    private static EquipmentItem ActiveItem()
    {
        var item = EquipmentItem.Register("EQP-2026-0001", "Analytical balance", "SN-100", "Lab 1", 180, 14);
        item.LogCalibration(Today, "Metrology Co", "Pass", null);
        return item;
    }

    [Fact]
    public void Registration_requires_first_calibration_to_activate()
    {
        var item = EquipmentItem.Register("EQP-2026-0001", "Balance", "SN-100", null, 180, 14);
        item.Status.Should().Be(EquipmentStatus.NeedsCalibration);

        item.LogCalibration(Today, "Metrology Co", "Pass", null);

        item.Status.Should().Be(EquipmentStatus.Active);
        item.NextCalibrationDue.Should().Be(Today.AddDays(180));
        item.DomainEvents.OfType<EquipmentReturnedToService>().Should().ContainSingle();
    }

    [Fact]
    public void Sweep_proposal_marks_due_only_when_actually_due()
    {
        var item = ActiveItem();

        item.MarkCalibrationDue(Today.AddDays(10)); // not due yet — declined
        item.Status.Should().Be(EquipmentStatus.Active);

        item.MarkCalibrationDue(Today.AddDays(180)); // due date reached
        item.Status.Should().Be(EquipmentStatus.NeedsCalibration);
        item.DomainEvents.OfType<CalibrationDue>().Should().ContainSingle();
    }

    [Fact]
    public void Lockout_only_after_grace_exhausted()
    {
        var item = ActiveItem();
        var due = Today.AddDays(180);
        item.MarkCalibrationDue(due);

        item.LockOutIfGraceExhausted(due.AddDays(14)); // last grace day — still allowed
        item.Status.Should().Be(EquipmentStatus.NeedsCalibration);

        item.LockOutIfGraceExhausted(due.AddDays(15)); // grace exhausted
        item.Status.Should().Be(EquipmentStatus.OutOfService);
        item.DomainEvents.OfType<EquipmentLockedOut>().Should().ContainSingle();
    }

    [Fact]
    public void Recalibration_returns_locked_out_equipment_to_service()
    {
        var item = ActiveItem();
        item.MarkCalibrationDue(Today.AddDays(180));
        item.LockOutIfGraceExhausted(Today.AddDays(200));

        item.LogCalibration(Today.AddDays(201), "Metrology Co", "Pass", null);

        item.Status.Should().Be(EquipmentStatus.Active);
        item.NextCalibrationDue.Should().Be(Today.AddDays(201 + 180));
    }

    [Fact]
    public void Retired_equipment_rejects_everything()
    {
        var item = ActiveItem();
        item.Retire();

        var calibrate = () => item.LogCalibration(Today, "x", "Pass", null);
        calibrate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("EQP-010");
    }
}

public class CompetencyRecordTests
{
    private static readonly Guid Trainee = Guid.CreateVersion7();
    private static readonly Guid Assessor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 7, 22);

    private static CompetencyRecord NewRecord() =>
        CompetencyRecord.Assign(Trainee, "SOP-CAL-045 balance calibration", null, 12);

    [Fact]
    public void Below_pass_mark_loops_back_to_pending_training()
    {
        var record = NewRecord();
        record.ScoreAssessment(79, Assessor, Now);

        record.Status.Should().Be(CompetencyStatus.PendingTraining);

        record.ScoreAssessment(85, Assessor, Now);
        record.Status.Should().Be(CompetencyStatus.Evaluated);
        record.Assessments.Should().HaveCount(2, "attempts are append-only");
    }

    [Fact]
    public void Trainee_cannot_assess_or_authorize_self()
    {
        var record = NewRecord();

        var selfScore = () => record.ScoreAssessment(95, Trainee, Now);
        selfScore.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-COMP-001");

        record.ScoreAssessment(95, Assessor, Now);
        var selfAuthorize = () => record.Authorize(Trainee, Today);
        selfAuthorize.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-COMP-001");
    }

    [Fact]
    public void Authorization_requires_evaluated_state_and_sets_expiry()
    {
        var record = NewRecord();

        var premature = () => record.Authorize(Assessor, Today);
        premature.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("COMP-012");

        record.ScoreAssessment(90, Assessor, Now);
        record.Authorize(Assessor, Today);

        record.Status.Should().Be(CompetencyStatus.Authorized);
        record.ExpiresAt.Should().Be(Today.AddMonths(12));
        record.DomainEvents.OfType<CompetencyAuthorized>().Should().ContainSingle();
    }

    [Fact]
    public void Expiry_returns_to_pending_training_for_requalification()
    {
        var record = NewRecord();
        record.ScoreAssessment(90, Assessor, Now);
        record.Authorize(Assessor, Today);

        record.ExpireIfDue(Today.AddMonths(11)); // not yet — declined
        record.Status.Should().Be(CompetencyStatus.Authorized);

        record.ExpireIfDue(Today.AddMonths(12));
        record.Status.Should().Be(CompetencyStatus.PendingTraining);
        record.DomainEvents.OfType<CompetencyExpired>().Should().ContainSingle();
    }

    [Fact]
    public void Revocation_is_terminal_and_requires_reason()
    {
        var record = NewRecord();
        record.ScoreAssessment(90, Assessor, Now);
        record.Authorize(Assessor, Today);

        var noReason = () => record.Revoke(Assessor, " ");
        noReason.Should().Throw<DomainException>().Which.Code.Should().Be("COMP-014");

        record.Revoke(Assessor, "Repeated deviation from method");
        record.Status.Should().Be(CompetencyStatus.Revoked);

        var rescore = () => record.ScoreAssessment(90, Assessor, Now);
        rescore.Should().Throw<InvalidStateTransitionException>();
    }
}
