using FluentAssertions;
using NT.QAMS.Domain.IdentityAccess;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.IdentityAccess;

public class UserAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount TenantUser() =>
        UserAccount.Create(Guid.CreateVersion7(), "qa@lab.test", "QA", "hash", UserRole.QualityManager);

    [Fact]
    public void Platform_admin_must_have_no_tenant_and_tenant_user_must_have_one()
    {
        var badAdmin = () => UserAccount.Create(Guid.CreateVersion7(), "a@x.test", "A", "h", UserRole.PlatformAdmin);
        badAdmin.Should().Throw<SharedKernel.Primitives.DomainException>().Which.Code.Should().Be("USER-003");

        var badTenantUser = () => UserAccount.Create(null, "a@x.test", "A", "h", UserRole.Analyst);
        badTenantUser.Should().Throw<SharedKernel.Primitives.DomainException>().Which.Code.Should().Be("USER-004");
    }

    [Fact]
    public void Fifth_failed_login_locks_the_account_for_30_minutes()
    {
        var user = TenantUser();

        for (var i = 0; i < 4; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        user.IsLockedOut(Now).Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(4);

        user.RegisterFailedLogin(Now); // 5th
        user.IsLockedOut(Now).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(31)).Should().BeFalse("the 30-minute lock has elapsed");
        user.DomainEvents.OfType<UserLockedOut>().Should().ContainSingle();
    }

    [Fact]
    public void Successful_login_resets_the_failure_counter_and_lock()
    {
        var user = TenantUser();
        user.RegisterFailedLogin(Now);
        user.RegisterFailedLogin(Now);

        user.RegisterSuccessfulLogin();

        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public void Mfa_is_not_enabled_until_confirmed()
    {
        var user = TenantUser();

        var confirmBeforeEnroll = () => user.ConfirmMfa();
        confirmBeforeEnroll.Should().Throw<SharedKernel.Primitives.DomainException>().Which.Code.Should().Be("MFA-002");

        user.EnrollMfa("JBSWY3DPEHPK3PXP");
        user.MfaEnabled.Should().BeFalse("enrollment alone does not enable MFA");

        user.ConfirmMfa();
        user.MfaEnabled.Should().BeTrue();
    }
}
