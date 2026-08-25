using FluentAssertions;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.IncidentReporting;

public class IncidentTests
{
    private static readonly Guid Reporter = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly Guid Investigator = Guid.CreateVersion7();
    private static readonly DateTimeOffset Occurred = new(2026, 8, 20, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static Incident Reported(HarmGrade harm = HarmGrade.Minor) =>
        Incident.Report(
            "INC-2026-0001", "Patient fall in ward B", "Unwitnessed fall from bed",
            IncidentCategory.Fall, harm, IntakeChannel.Web, Occurred, Reporter, "Ward B");

    private static Incident PendingReview()
    {
        var incident = Reported();
        incident.Triage(Manager, IncidentCategory.Fall);
        incident.StartInvestigation(Investigator);
        incident.RecordInvestigationSummary("Bed rails were down; call bell out of reach.");
        incident.SubmitForReview();
        return incident;
    }

    [Fact]
    public void Report_starts_in_reported_and_raises_reported_event()
    {
        var incident = Reported();

        incident.Status.Should().Be(IncidentStatus.Reported);
        incident.IsAnonymous.Should().BeFalse();
        incident.ReportedBy.Should().Be(Reporter);
        incident.DomainEvents.OfType<IncidentReported>().Should().ContainSingle();
    }

    [Fact]
    public void Report_requires_a_title()
    {
        var act = () => Incident.Report(
            "INC-1", " ", "D", IncidentCategory.Other, HarmGrade.NoHarm, IntakeChannel.Web, Occurred, Reporter);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("INC-001");
    }

    [Fact]
    public void Report_requires_an_occurrence_time()
    {
        var act = () => Incident.Report(
            "INC-1", "T", "D", IncidentCategory.Other, HarmGrade.NoHarm, IntakeChannel.Web, default, Reporter);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("INC-002");
    }

    [Theory]
    [InlineData(HarmGrade.Severe)]
    [InlineData(HarmGrade.Death)]
    public void Severe_and_death_grades_auto_escalate_on_report(HarmGrade harm)
    {
        var incident = Reported(harm);
        incident.DomainEvents.OfType<IncidentEscalated>().Should().ContainSingle()
            .Which.HarmGrade.Should().Be(harm);
    }

    [Theory]
    [InlineData(HarmGrade.NearMiss)]
    [InlineData(HarmGrade.Minor)]
    [InlineData(HarmGrade.Moderate)]
    public void Lower_grades_do_not_auto_escalate(HarmGrade harm)
    {
        Reported(harm).DomainEvents.OfType<IncidentEscalated>().Should().BeEmpty();
    }

    [Fact]
    public void Anonymous_report_stores_no_reporter_and_only_the_reference_hash()
    {
        var incident = Incident.ReportAnonymous(
            "INC-2026-0002", "Medication near miss", "Wrong dose caught before administration",
            IncidentCategory.Medication, HarmGrade.NearMiss, IntakeChannel.Kiosk, Occurred, new string('a', 64));

        incident.IsAnonymous.Should().BeTrue();
        incident.ReportedBy.Should().BeNull();
        incident.AnonymousReferenceHash.Should().Be(new string('a', 64));
    }

    [Fact]
    public void Anonymous_report_requires_a_reference()
    {
        var act = () => Incident.ReportAnonymous(
            "INC-1", "T", "D", IncidentCategory.Other, HarmGrade.NoHarm, IntakeChannel.Kiosk, Occurred, " ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("INC-004");
    }

    [Fact]
    public void Full_happy_path_reaches_closed()
    {
        var incident = Reported();
        incident.Triage(Manager, IncidentCategory.Fall);
        incident.StartInvestigation(Investigator);
        incident.AddContributingFactor(ContributingFactorCategory.Environment, "Bed rails down");
        incident.AddTimelineEntry(Occurred, "Found on floor at 09:30", Investigator);
        incident.RecordInvestigationSummary("Preventable; rails protocol not followed.");
        incident.SubmitForReview();
        incident.Close("Corrective actions raised; family informed.", Manager);

        incident.Status.Should().Be(IncidentStatus.Closed);
        incident.ContributingFactors.Should().ContainSingle();
        incident.Timeline.Should().ContainSingle();
        incident.DomainEvents.OfType<IncidentClosed>().Should().ContainSingle();
    }

    [Fact]
    public void Triage_only_from_reported()
    {
        var incident = PendingReview();
        var act = () => incident.Triage(Manager, IncidentCategory.Fall);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("INC-010");
    }

    [Fact]
    public void Submit_for_review_requires_an_investigation_summary()
    {
        var incident = Reported();
        incident.Triage(Manager, IncidentCategory.Fall);
        incident.StartInvestigation(Investigator);

        var act = incident.SubmitForReview;

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INC-023");
        incident.Status.Should().Be(IncidentStatus.UnderInvestigation);
    }

    [Fact]
    public void Contributing_factors_only_during_investigation()
    {
        var incident = Reported();
        var act = () => incident.AddContributingFactor(ContributingFactorCategory.People, "x");
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("INC-016");
    }

    [Fact]
    public void Close_requires_a_summary()
    {
        var incident = PendingReview();
        var act = () => incident.Close(" ", Manager);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("INC-025");
        incident.Status.Should().Be(IncidentStatus.PendingReview);
    }

    [Fact]
    public void Segregation_of_duties_reporter_cannot_close_own_incident()
    {
        var incident = PendingReview();

        var act = () => incident.Close("Closing my own report", Reporter);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-INC-001");
        incident.Status.Should().Be(IncidentStatus.PendingReview, "the illegal close must not change state");
    }

    [Fact]
    public void Anonymous_incident_has_no_reporter_so_anyone_may_close()
    {
        var incident = Incident.ReportAnonymous(
            "INC-2026-0003", "Security breach", "Door left unsecured",
            IncidentCategory.Security, HarmGrade.NoHarm, IntakeChannel.Web, Occurred, new string('b', 64));
        incident.Triage(Manager, IncidentCategory.Security);
        incident.StartInvestigation(Investigator);
        incident.RecordInvestigationSummary("Door closer faulty.");
        incident.SubmitForReview();

        incident.Close("Fixed and audited.", Manager);

        incident.Status.Should().Be(IncidentStatus.Closed);
    }

    [Fact]
    public void Declare_sentinel_flags_record_and_raises_event()
    {
        var incident = Reported(HarmGrade.Death);

        incident.DeclareSentinel(Manager, Now);

        incident.IsSentinel.Should().BeTrue();
        incident.SentinelDeclaredAtUtc.Should().Be(Now);
        incident.DomainEvents.OfType<SentinelDeclared>().Should().ContainSingle();
    }

    [Fact]
    public void Cannot_declare_sentinel_twice()
    {
        var incident = Reported(HarmGrade.Severe);
        incident.DeclareSentinel(Manager, Now);

        var act = () => incident.DeclareSentinel(Manager, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INC-027");
    }

    [Fact]
    public void Cannot_declare_sentinel_on_a_closed_incident()
    {
        var incident = PendingReview();
        incident.Close("Closed", Manager);

        var act = () => incident.DeclareSentinel(Manager, Now);

        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("INC-026");
    }

    [Fact]
    public void Reject_only_from_reported_and_requires_reason()
    {
        var incident = Reported();

        var noReason = () => incident.Reject(" ");
        noReason.Should().Throw<DomainException>().Which.Code.Should().Be("INC-013");

        incident.Reject("Duplicate of INC-2026-0000");
        incident.Status.Should().Be(IncidentStatus.Rejected);
        incident.DomainEvents.OfType<IncidentRejected>().Should().ContainSingle();
    }

    [Fact]
    public void Link_corrective_action_is_idempotent_first_link_wins()
    {
        var incident = Reported();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        incident.LinkCorrectiveAction(first);
        incident.LinkCorrectiveAction(second);

        incident.CorrectiveActionNcId.Should().Be(first);
    }

    [Fact]
    public void Cannot_raise_corrective_action_from_a_rejected_incident()
    {
        var incident = Reported();
        incident.Reject("Not an incident");

        var act = () => incident.LinkCorrectiveAction(Guid.CreateVersion7());

        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("INC-030");
    }
}
