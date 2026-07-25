using FluentAssertions;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Equipment;

public sealed class ReferenceStandardTests
{
    private static readonly DateOnly Received = new(2026, 1, 10);

    private static ReferenceStandard NewStandard(DateOnly? expires = null) => ReferenceStandard.Register(
        "RS-2026-0001", "Glucose CRM", ReferenceStandardType.CertifiedReferenceMaterial,
        "SI via NIST SRM 917c", "NIST", "Lot 42", "CERT-917c-42",
        "5.51 mmol/L", "±0.05 mmol/L (k=2)", Received, expires ?? new DateOnly(2027, 1, 10));

    [Fact]
    public void Registration_requires_a_traceability_chain_and_a_sane_expiry()
    {
        var untraceable = () => ReferenceStandard.Register(
            "RS-1", "X", ReferenceStandardType.WorkingStandard, " ",
            null, null, null, null, null, Received, null);
        untraceable.Should().Throw<DomainException>().Which.Code.Should().Be("RS-002");

        var expiresBeforeReceipt = () => ReferenceStandard.Register(
            "RS-1", "X", ReferenceStandardType.WorkingStandard, "SI via PTB",
            null, null, null, null, null, Received, Received);
        expiresBeforeReceipt.Should().Throw<DomainException>().Which.Code.Should().Be("RS-003");

        NewStandard().Status.Should().Be(ReferenceStandardStatus.Active);
    }

    [Fact]
    public void Quarantine_requires_a_reason_and_reactivation_checks_the_certificate()
    {
        var standard = NewStandard(expires: new DateOnly(2026, 6, 1));

        var noReason = () => standard.Quarantine(" ");
        noReason.Should().Throw<DomainException>().Which.Code.Should().Be("RS-011");

        standard.Quarantine("Dropped during transport");
        standard.Status.Should().Be(ReferenceStandardStatus.Quarantined);
        standard.DomainEvents.Should().ContainSingle(e => e is ReferenceStandardQuarantined);

        // Reactivating after the certificate expired is refused (RS-013).
        var lateReactivate = () => standard.Reactivate(new DateOnly(2026, 7, 1));
        lateReactivate.Should().Throw<DomainException>().Which.Code.Should().Be("RS-013");

        standard.Reactivate(new DateOnly(2026, 5, 1));
        standard.Status.Should().Be(ReferenceStandardStatus.Active);
        standard.QuarantineReason.Should().BeNull();
    }

    [Fact]
    public void Expiry_sweep_latches_only_when_actually_due_and_raises_the_event()
    {
        var standard = NewStandard(expires: new DateOnly(2026, 6, 1));

        standard.MarkExpiredIfReached(new DateOnly(2026, 5, 31));
        standard.Status.Should().Be(ReferenceStandardStatus.Active); // Proposal declined.

        standard.MarkExpiredIfReached(new DateOnly(2026, 6, 1));
        standard.Status.Should().Be(ReferenceStandardStatus.Expired);
        standard.DomainEvents.Should().ContainSingle(e => e is ReferenceStandardExpired);
    }

    [Fact]
    public void Retire_is_terminal()
    {
        var standard = NewStandard();
        standard.Retire();
        standard.Status.Should().Be(ReferenceStandardStatus.Retired);

        var again = () => standard.Retire();
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("RS-014");
    }
}
