using FluentAssertions;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Tenancy;

public class TenantTests
{
    private static Tenant NewTenant() =>
        Tenant.Provision(TenantSlug.Create("amman-central-lab"), "Amman Central Laboratory");

    [Fact]
    public void Provision_creates_active_tenant_and_raises_event()
    {
        var tenant = NewTenant();

        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.Settings.Should().Be(TenantSettings.Default);
        tenant.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TenantProvisioned>()
            .Which.Slug.Should().Be("amman-central-lab");
    }

    [Fact]
    public void Provision_rejects_blank_name()
    {
        var act = () => Tenant.Provision(TenantSlug.Create("lab"), "   ");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("TENANT-003");
    }

    [Fact]
    public void Suspend_requires_reason_and_active_state()
    {
        var tenant = NewTenant();

        var noReason = () => tenant.Suspend("");
        noReason.Should().Throw<DomainException>().Which.Code.Should().Be("TENANT-011");

        tenant.Suspend("Non-payment");
        tenant.Status.Should().Be(TenantStatus.Suspended);
        tenant.SuspensionReason.Should().Be("Non-payment");

        var again = () => tenant.Suspend("twice");
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("TENANT-010");
    }

    [Fact]
    public void Reactivate_only_from_suspended()
    {
        var tenant = NewTenant();

        var fromActive = () => tenant.Reactivate();
        fromActive.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("TENANT-012");

        tenant.Suspend("audit");
        tenant.Reactivate();

        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public void Terminate_is_terminal()
    {
        var tenant = NewTenant();
        tenant.Terminate();

        tenant.Status.Should().Be(TenantStatus.Terminated);

        var again = () => tenant.Terminate();
        again.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("TENANT-013");
    }

    [Theory]
    [InlineData("Valid-Lab-01", "valid-lab-01")]
    [InlineData("  ACME  ", "acme")]
    public void Slug_normalizes_case_and_whitespace(string input, string expected) =>
        TenantSlug.Create(input).Value.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("no--double")]
    [InlineData("has space")]
    [InlineData("UPPER_underscore")]
    public void Slug_rejects_invalid_identifiers(string input)
    {
        var act = () => TenantSlug.Create(input);
        act.Should().Throw<DomainException>();
    }
}
