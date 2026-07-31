using FluentAssertions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.IdentityAccess;

/// <summary>
/// The user side of the privilege module: role assignment, the branch/department
/// working scope (empty = unrestricted), and the preferred language.
/// </summary>
public sealed class UserScopeTests
{
    private static UserAccount NewUser() => UserAccount.Create(
        Guid.CreateVersion7(), "analyst@lab.test", "Analyst", "hash", UserRole.Analyst);

    [Fact]
    public void Assigning_a_role_raises_the_access_control_event()
    {
        var user = NewUser();
        var roleId = Guid.CreateVersion7();

        user.AssignRole(roleId);

        user.RoleId.Should().Be(roleId);
        user.DomainEvents.OfType<UserRoleAssigned>().Should().ContainSingle()
            .Which.RoleId.Should().Be(roleId);
    }

    [Fact]
    public void An_empty_role_id_is_rejected()
    {
        FluentActions.Invoking(() => NewUser().AssignRole(Guid.Empty))
            .Should().Throw<DomainException>().Which.Code.Should().Be("USER-010");
    }

    [Fact]
    public void Setting_a_scope_deduplicates_and_records_the_change()
    {
        var user = NewUser();
        var branch = Guid.CreateVersion7();
        var department = Guid.CreateVersion7();

        user.SetScope([branch, branch, Guid.Empty], [department]);

        user.BranchAccess.Should().ContainSingle().Which.BranchId.Should().Be(branch);
        user.DepartmentAccess.Should().ContainSingle().Which.DepartmentId.Should().Be(department);
        user.HasUnrestrictedBranchAccess.Should().BeFalse();
        user.HasUnrestrictedDepartmentAccess.Should().BeFalse();

        var change = user.DomainEvents.OfType<UserScopeChanged>().Should().ContainSingle().Subject;
        change.IsUnrestricted.Should().BeFalse();
        change.BranchIds.Should().Equal(branch);
    }

    [Fact]
    public void Clearing_the_scope_is_recorded_as_the_explicit_unrestricted_case()
    {
        var user = NewUser();
        user.SetScope([Guid.CreateVersion7()], []);
        user.ClearDomainEvents();

        user.SetScope([], []);

        user.HasUnrestrictedBranchAccess.Should().BeTrue();
        user.HasUnrestrictedDepartmentAccess.Should().BeTrue();
        user.DomainEvents.OfType<UserScopeChanged>().Should().ContainSingle()
            .Which.IsUnrestricted.Should().BeTrue();
    }

    [Fact]
    public void Preferred_language_is_normalized_and_nullable()
    {
        var user = NewUser();

        user.SetPreferredLanguage("  AR ");
        user.PreferredLanguage.Should().Be("ar");

        user.SetPreferredLanguage("   ");
        user.PreferredLanguage.Should().BeNull("blank means inherit role, then tenant");
    }
}
