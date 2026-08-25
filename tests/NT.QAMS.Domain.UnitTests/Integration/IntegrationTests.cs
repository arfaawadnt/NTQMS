using FluentAssertions;
using NT.QAMS.Domain.Integration;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Integration;

public class IntegrationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    // ── Endpoint health ──────────────────────────────────────────────────────

    [Fact]
    public void Consecutive_failures_reaching_threshold_raise_an_alert()
    {
        var e = IntegrationEndpoint.Register("HIS ADT", InterfaceSystem.His, InterfaceProtocol.Hl7V2);
        e.RecordFailure(T0);
        e.RecordFailure(T0);
        e.IsHealthy.Should().BeTrue("two failures is below the threshold of three");

        e.RecordFailure(T0);
        e.IsHealthy.Should().BeFalse();
        e.DomainEvents.OfType<InterfaceUnhealthy>().Should().ContainSingle();
    }

    [Fact]
    public void A_success_clears_the_failure_streak()
    {
        var e = IntegrationEndpoint.Register("LIS", InterfaceSystem.Lis, InterfaceProtocol.FhirR4);
        e.RecordFailure(T0);
        e.RecordFailure(T0);
        e.RecordSuccess(T0);
        e.ConsecutiveFailures.Should().Be(0);
        e.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void A_suspended_endpoint_is_not_healthy_and_resume_clears_failures()
    {
        var e = IntegrationEndpoint.Register("Pharmacy", InterfaceSystem.Pharmacy, InterfaceProtocol.DbExtract);
        e.RecordFailure(T0);
        e.Suspend();
        e.IsHealthy.Should().BeFalse();
        e.Resume();
        e.ConsecutiveFailures.Should().Be(0);
        e.IsHealthy.Should().BeTrue();
    }

    // ── Inbox message ────────────────────────────────────────────────────────

    [Fact]
    public void A_message_records_and_transitions_processed()
    {
        var m = IntegrationMessage.Receive(Guid.CreateVersion7(), "MSG-1", "ADT^A01", "raw", T0);
        m.Status.Should().Be(MessageStatus.Received);
        m.MarkProcessed(T0);
        m.Status.Should().Be(MessageStatus.Processed);
        m.ProcessedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void A_message_requires_a_dedup_key()
    {
        var act = () => IntegrationMessage.Receive(Guid.CreateVersion7(), " ", "ADT^A01", "raw", T0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MSG-002");
    }

    // ── Patient stay projection ──────────────────────────────────────────────

    [Fact]
    public void Patient_days_count_from_admission_to_discharge()
    {
        var stay = PatientStay.Admit("PT-001", "ENC-001", "Ward A", null, T0);
        stay.Discharge(T0.AddDays(5));
        stay.PatientDays(T0.AddDays(10)).Should().Be(5, "discharge caps the count regardless of as-of");
    }

    [Fact]
    public void An_admitted_stay_accrues_days_up_to_the_as_of()
    {
        var stay = PatientStay.Admit("PT-002", "ENC-002", "ICU", null, T0);
        stay.PatientDays(T0.AddDays(3)).Should().Be(3);
    }

    [Fact]
    public void Same_day_stay_counts_as_one_patient_day()
    {
        var stay = PatientStay.Admit("PT-003", "ENC-003", "ER", null, T0);
        stay.PatientDays(T0.AddHours(6)).Should().Be(1);
    }

    [Fact]
    public void Discharge_cannot_precede_admission()
    {
        var stay = PatientStay.Admit("PT-004", "ENC-004", "Ward B", null, T0);
        var act = () => stay.Discharge(T0.AddDays(-1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("STAY-011");
    }

    [Fact]
    public void Repeated_discharge_is_idempotent()
    {
        var stay = PatientStay.Admit("PT-005", "ENC-005", "Ward C", null, T0);
        stay.Discharge(T0.AddDays(2));
        stay.Discharge(T0.AddDays(3)); // no-op
        stay.DischargedAtUtc.Should().Be(T0.AddDays(2));
    }
}
