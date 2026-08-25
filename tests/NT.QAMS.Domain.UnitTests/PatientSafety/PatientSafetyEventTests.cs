using FluentAssertions;
using NT.QAMS.Domain.PatientSafety;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.PatientSafety;

public class PatientSafetyEventTests
{
    private static readonly Guid Reviewer = Guid.CreateVersion7();
    private static readonly DateTimeOffset When = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_fall_is_hospital_acquired_and_has_no_stage()
    {
        var e = PatientSafetyEvent.ReportFall("PSE-1", "PT-1", "Ward A", When, HarmLevel.Minor, "Slipped");
        e.Origin.Should().Be(InjuryOrigin.HospitalAcquired);
        e.Stage.Should().BeNull();
        e.IsHospitalAcquiredPressureInjury.Should().BeFalse();
    }

    [Fact]
    public void A_hospital_acquired_pressure_injury_is_flagged()
    {
        var e = PatientSafetyEvent.ReportPressureInjury(
            "PSE-2", "PT-2", "ICU", When, HarmLevel.Moderate, "Sacral",
            PressureInjuryStage.Stage3, InjuryOrigin.HospitalAcquired);
        e.Stage.Should().Be(PressureInjuryStage.Stage3);
        e.IsHospitalAcquiredPressureInjury.Should().BeTrue();
    }

    [Fact]
    public void A_present_on_admission_pressure_injury_is_not_a_hapi()
    {
        var e = PatientSafetyEvent.ReportPressureInjury(
            "PSE-3", "PT-3", "Ward B", When, HarmLevel.Minor, "Heel",
            PressureInjuryStage.Stage2, InjuryOrigin.PresentOnAdmission);
        e.IsHospitalAcquiredPressureInjury.Should().BeFalse();
    }

    [Fact]
    public void Review_requires_notes_and_only_from_reported()
    {
        var e = PatientSafetyEvent.ReportFall("PSE-4", "PT-4", "Ward A", When, HarmLevel.None, "No harm");

        var noNotes = () => e.RecordReview(Reviewer, " ", When);
        noNotes.Should().Throw<DomainException>().Which.Code.Should().Be("PSE-011");

        e.RecordReview(Reviewer, "Bed rails were down; protocol reinforced.", When);
        e.Status.Should().Be(SafetyEventStatus.Reviewed);

        var again = () => e.RecordReview(Reviewer, "x", When);
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("PSE-010");
    }

    [Fact]
    public void Close_requires_review_first()
    {
        var e = PatientSafetyEvent.ReportFall("PSE-5", "PT-5", "Ward A", When, HarmLevel.Minor, "x");

        var early = e.Close;
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("PSE-012");

        e.RecordReview(Reviewer, "Reviewed.", When);
        e.Close();
        e.Status.Should().Be(SafetyEventStatus.Closed);
    }
}
