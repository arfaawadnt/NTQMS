using FluentAssertions;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Authorization;

/// <summary>
/// The Role aggregate: tenant-composable privilege sets whose every change is a
/// regulated record (21 CFR Part 11 §11.10 — who may do what is itself
/// controlled), with system-role protections and closed-catalogue validation.
/// </summary>
public sealed class RoleTests
{
    private static readonly string View = PermissionCatalog.Key(
        PermissionCatalog.Nonconformances, PermissionAction.View);

    private static readonly string Approve = PermissionCatalog.Key(
        PermissionCatalog.Nonconformances, PermissionAction.Approve);

    [Fact]
    public void Creating_normalizes_grants_and_raises_the_event()
    {
        var role = Role.Create("  Lab Supervisor ", "Supervises the bench.", [$"  {View.ToUpperInvariant()} ", View]);

        role.Name.Should().Be("Lab Supervisor");
        role.NormalizedName.Should().Be("LAB SUPERVISOR");
        role.IsActive.Should().BeTrue();
        role.IsSystem.Should().BeFalse();
        role.PermissionKeys.Should().ContainSingle().Which.Should().Be(View);
        role.DomainEvents.OfType<RoleCreated>().Should().ContainSingle();
    }

    [Fact]
    public void A_key_outside_the_catalogue_is_rejected()
    {
        var act = () => Role.Create("Ghost", null, ["nc.frobnicate"]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ROLE-005");
    }

    [Fact]
    public void An_empty_or_overlong_name_is_rejected()
    {
        FluentActions.Invoking(() => Role.Create("  ", null, [View]))
            .Should().Throw<DomainException>().Which.Code.Should().Be("ROLE-001");
        FluentActions.Invoking(() => Role.Create(new string('x', 81), null, [View]))
            .Should().Throw<DomainException>().Which.Code.Should().Be("ROLE-002");
    }

    [Fact]
    public void Changing_permissions_records_exactly_the_grants_and_revocations()
    {
        var role = Role.Create("Reviewer", null, [View]);

        role.SetPermissions([Approve], "Reviewer now approves NCs instead of only reading them.");

        var change = role.DomainEvents.OfType<RolePermissionsChanged>().Should().ContainSingle().Subject;
        change.Granted.Should().ContainSingle().Which.Should().Be(Approve);
        change.Revoked.Should().ContainSingle().Which.Should().Be(View);
        change.Reason.Should().Contain("approves");
        role.Grants(Approve).Should().BeTrue();
        role.Grants(View).Should().BeFalse();
    }

    [Fact]
    public void An_unchanged_permission_set_raises_no_event()
    {
        var role = Role.Create("Reader", null, [View]);

        role.SetPermissions([View.ToUpperInvariant()], "No-op resubmission of the same set.");

        role.DomainEvents.OfType<RolePermissionsChanged>().Should().BeEmpty();
    }

    [Fact]
    public void System_roles_cannot_be_renamed_or_deactivated_but_can_be_retuned()
    {
        var role = Role.CreateSystem("Quality Manager", "Seeded.", [View]);

        FluentActions.Invoking(() => role.Rename("QM", null))
            .Should().Throw<DomainException>().Which.Code.Should().Be("ROLE-003");
        FluentActions.Invoking(role.Deactivate)
            .Should().Throw<DomainException>().Which.Code.Should().Be("ROLE-004");

        role.SetPermissions([Approve], "Tenant tunes the seeded role.");
        role.Grants(Approve).Should().BeTrue();
    }

    [Fact]
    public void Deactivation_and_reactivation_are_idempotent_and_evented()
    {
        var role = Role.Create("Temp", null, [View]);

        role.Deactivate();
        role.Deactivate();
        role.IsActive.Should().BeFalse();
        role.DomainEvents.OfType<RoleDeactivated>().Should().ContainSingle();

        role.Reactivate();
        role.Reactivate();
        role.IsActive.Should().BeTrue();
        role.DomainEvents.OfType<RoleReactivated>().Should().ContainSingle();
    }

    [Fact]
    public void Renaming_a_tenant_role_updates_the_normalized_name()
    {
        var role = Role.Create("Old Name", null, [View]);

        role.Rename("New Name", "Updated.");

        role.NormalizedName.Should().Be("NEW NAME");
        role.DomainEvents.OfType<RoleRenamed>().Should().ContainSingle();
    }
}

/// <summary>
/// The permission catalogue is a stable persisted contract: keys are
/// lower-case module.action, unique, and the special <c>roles.manage</c> key —
/// referenced from attribute arguments as a literal — must never drift.
/// </summary>
public sealed class PermissionCatalogTests
{
    [Fact]
    public void Every_key_is_lowercase_module_dot_action_and_unique()
    {
        PermissionCatalog.AllKeys.Should().NotBeEmpty();
        PermissionCatalog.AllKeys.Should().OnlyContain(k => k == k.ToLowerInvariant() && k.Contains('.'));
        PermissionCatalog.Modules.Select(m => m.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ManageRoles_is_the_pinned_literal_used_in_policy_attributes()
    {
        PermissionCatalog.ManageRoles.Should().Be("roles.manage");
        PermissionCatalog.IsKnown(PermissionCatalog.ManageRoles).Should().BeTrue();
    }

    [Fact]
    public void Unknown_keys_are_not_known()
    {
        PermissionCatalog.IsKnown("nc.frobnicate").Should().BeFalse();
        PermissionCatalog.IsKnown("NC.VIEW").Should().BeFalse("keys are case-sensitive lower-case");
    }

    [Fact]
    public void Every_module_declares_view_so_the_matrix_always_has_a_read_column()
    {
        PermissionCatalog.Modules.Should().OnlyContain(m => m.Actions.Contains(PermissionAction.View));
    }
}
