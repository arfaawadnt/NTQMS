using FluentAssertions;
using NT.QAMS.Domain.Competency;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Competency;

public sealed class TestAuthorizationTests
{
    private static readonly Guid Analyst = Guid.CreateVersion7();
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly Guid Test = Guid.CreateVersion7();
    private static readonly Guid Competency = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 7, 25);

    private static TestAuthorization Granted(DateOnly? expires = null) => TestAuthorization.Grant(
        Analyst, Test, Competency, AuthorizationScope.Perform, Qm, Today, expires ?? Today.AddMonths(12));

    [Fact]
    public void Grant_enforces_sod_and_a_future_expiry()
    {
        var selfGrant = () => TestAuthorization.Grant(
            Analyst, Test, Competency, AuthorizationScope.Perform, Analyst, Today, Today.AddMonths(12));
        selfGrant.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-AUTHZ-001");

        var lapsed = () => TestAuthorization.Grant(
            Analyst, Test, Competency, AuthorizationScope.Perform, Qm, Today, Today);
        lapsed.Should().Throw<DomainException>().Which.Code.Should().Be("AUTHZ-001");

        Granted().Status.Should().Be(TestAuthorizationStatus.Active);
    }

    [Fact]
    public void Suspension_is_reversible_but_not_past_expiry()
    {
        var authorization = Granted(expires: new DateOnly(2026, 9, 1));

        authorization.Suspend("Competency under review");
        authorization.Status.Should().Be(TestAuthorizationStatus.Suspended);

        var lateReinstate = () => authorization.Reinstate(new DateOnly(2026, 9, 1));
        lateReinstate.Should().Throw<DomainException>().Which.Code.Should().Be("AUTHZ-013");

        authorization.Reinstate(new DateOnly(2026, 8, 1));
        authorization.Status.Should().Be(TestAuthorizationStatus.Active);
        authorization.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public void Saga_shaped_suspend_skips_entries_already_off_the_active_path()
    {
        var authorization = Granted();
        authorization.Revoke(Qm, "Misconduct");

        authorization.SuspendIfActive("Competency expired"); // Must not throw.
        authorization.Status.Should().Be(TestAuthorizationStatus.Revoked);
    }

    [Fact]
    public void Revocation_is_terminal_and_raises_the_event()
    {
        var authorization = Granted();
        authorization.Revoke(Qm, "Competency revoked: falsified records");

        authorization.Status.Should().Be(TestAuthorizationStatus.Revoked);
        authorization.DomainEvents.Should().ContainSingle(e => e is TestAuthorizationRevoked);

        var again = () => authorization.Revoke(Qm, "again");
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("AUTHZ-014");
    }

    [Fact]
    public void Expiry_sweep_latches_active_and_suspended_entries_when_due()
    {
        var active = Granted(expires: new DateOnly(2026, 8, 1));
        active.ExpireIfDue(new DateOnly(2026, 7, 31));
        active.Status.Should().Be(TestAuthorizationStatus.Active); // Proposal declined.

        active.ExpireIfDue(new DateOnly(2026, 8, 1));
        active.Status.Should().Be(TestAuthorizationStatus.Expired);
        active.DomainEvents.Should().ContainSingle(e => e is TestAuthorizationExpired);

        var suspended = Granted(expires: new DateOnly(2026, 8, 1));
        suspended.Suspend("On hold");
        suspended.ExpireIfDue(new DateOnly(2026, 8, 2));
        suspended.Status.Should().Be(TestAuthorizationStatus.Expired);
    }
}
