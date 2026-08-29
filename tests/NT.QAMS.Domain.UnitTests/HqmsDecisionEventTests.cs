using System.Linq;
using FluentAssertions;
using NT.QAMS.Domain.Credentialing;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.Domain.MortalityReview;
using NT.QAMS.Domain.PatientSafety;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;
using MortalityReviewAggregate = NT.QAMS.Domain.MortalityReview.MortalityReview;

namespace NT.QAMS.Domain.UnitTests;

/// <summary>
/// Audit finding M-06: the regulated decision facts of the hospital modules now
/// raise domain events, so each reaches the hash-chained audit ledger through the
/// outbox. These pin the events at their decision points; the
/// <c>DomainEventsAreRaisedTests</c> architecture guard keeps the surface honest
/// going forward.
/// </summary>
public class HqmsDecisionEventTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mortality_classification_and_closure_raise_events()
    {
        var review = MortalityReviewAggregate.Report("MRT-1", "PT-1", "ICU", Now.AddDays(-1), "Sepsis");
        review.Classify(Actor, DeathClassification.Expected, "Expected end-of-life.");
        review.DomainEvents.OfType<MortalityClassified>().Should().ContainSingle()
            .Which.Classification.Should().Be("Expected");

        review.Close(); // Expected → no second review required
        review.DomainEvents.OfType<MortalityReviewClosed>().Should().ContainSingle();
    }

    [Fact]
    public void Drill_evaluation_raises_an_event()
    {
        var drill = Drill.Schedule("DRL-1", DrillType.Fire, "Ward A", Today);
        drill.Execute(Now, participantCount: 12);
        drill.Evaluate(85, "Good response time.");
        drill.DomainEvents.OfType<DrillEvaluated>().Should().ContainSingle()
            .Which.Score.Should().Be(85);
    }

    [Fact]
    public void Safety_round_completion_raises_an_event()
    {
        var round = SafetyRound.Schedule("RND-1", "Pharmacy", RoundType.GeneralSafety, Today);
        round.Start(Actor);
        round.Complete();
        round.DomainEvents.OfType<RoundCompleted>().Should().ContainSingle();
    }

    [Fact]
    public void Patient_safety_event_closure_raises_an_event()
    {
        var e = PatientSafetyEvent.ReportFall("PSE-1", "PT-1", "Ward A", Now.AddHours(-1), HarmLevel.Minor, "Fall");
        e.RecordReview(Actor, "Reviewed.", Now);
        e.Close();
        e.DomainEvents.OfType<SafetyEventClosed>().Should().ContainSingle();
    }

    [Fact]
    public void Privilege_grant_credentialing_and_suspension_raise_events()
    {
        var adder = Guid.CreateVersion7();
        var verifier = Guid.CreateVersion7();
        var p = Practitioner.Register("PRC-1", "Dr Roe", "Cardiology");
        var lic = p.AddLicence(CredentialType.MedicalLicence, "ML-1", "Council", Today.AddYears(1), adder);
        p.VerifyLicence(lic, verifier, "Council register", Now);
        var priv = p.RequestPrivilege("Angiography", Today);

        p.GrantPrivilege(priv, Today.AddYears(2));
        p.DomainEvents.OfType<PrivilegeGranted>().Should().ContainSingle();

        p.Credential(Today.AddYears(2), Today);
        p.DomainEvents.OfType<PractitionerCredentialed>().Should().ContainSingle();

        p.Suspend("Under investigation.");
        p.DomainEvents.OfType<PractitionerSuspended>().Should().ContainSingle();
    }
}
