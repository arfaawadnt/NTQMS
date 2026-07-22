using FluentAssertions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.IdentityAccess;

public class UserRoleAndResetTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount TenantUser() =>
        UserAccount.Create(Guid.CreateVersion7(), "u@lab.test", "User", "hash", UserRole.Analyst);

    [Fact]
    public void Change_role_updates_the_role()
    {
        var user = TenantUser();
        user.ChangeRole(UserRole.DepartmentHead);
        user.Role.Should().Be(UserRole.DepartmentHead);
    }

    [Fact]
    public void Tenant_user_cannot_be_made_platform_admin()
    {
        var user = TenantUser();
        var act = () => user.ChangeRole(UserRole.PlatformAdmin);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("USER-005");
    }

    [Fact]
    public void Reset_password_replaces_hash_and_clears_lockout()
    {
        var user = TenantUser();
        for (var i = 0; i < UserAccount.MaxFailedAttempts; i++) { user.RegisterFailedLogin(Now); }
        user.IsLockedOut(Now).Should().BeTrue();

        user.ResetPassword("new-hash");

        user.IsLockedOut(Now).Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public void Deactivate_and_reactivate_toggle_active()
    {
        var user = TenantUser();
        user.Deactivate();
        user.IsActive.Should().BeFalse();
        user.Reactivate();
        user.IsActive.Should().BeTrue();
    }
}
