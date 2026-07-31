using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Authorization;

/// <summary>
/// Defect RP-D1 (found by OQ-RP-09): user-account events — role assignment,
/// scope changes, lockouts — reached the audit ledger with an EMPTY tenant id,
/// because UserAccount is deliberately not ITenantScoped and the outbox drain
/// only looked there. The tenant whose access control changed could not see
/// the change in its own audit trail. These tests pin the fix.
/// </summary>
public class UserEventTenantStampTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    private static AppDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-tenant-{Guid.NewGuid()}")
            .AddInterceptors(new OutboxInterceptor(new FixedClock(Now)))
            .Options,
        new FakeCurrentTenant());

    [Fact]
    public async Task A_tenant_users_access_control_events_carry_the_owning_tenant()
    {
        await using var db = NewContext();
        var tenantId = Guid.CreateVersion7();
        var user = UserAccount.Create(tenantId, "analyst@lab.test", "Analyst", "hash", UserRole.Analyst);
        user.AssignRole(Guid.CreateVersion7());
        user.SetScope([Guid.CreateVersion7()], []);

        db.Users.Add(user);
        await db.SaveChangesAsync(CancellationToken.None);

        var rows = await db.Set<OutboxEvent>().ToListAsync();
        rows.Should().Contain(r => r.EventType.Contains(nameof(UserRoleAssigned)))
            .Which.TenantId.Should().Be(tenantId);
        rows.Should().Contain(r => r.EventType.Contains(nameof(UserScopeChanged)))
            .Which.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task A_platform_administrators_events_stay_platform_level()
    {
        await using var db = NewContext();
        var admin = UserAccount.Create(null, "root@platform.test", "Root", "hash", UserRole.PlatformAdmin);
        for (var i = 0; i < UserAccount.MaxFailedAttempts; i++)
        {
            admin.RegisterFailedLogin(Now); // 5th failure raises UserLockedOut
        }

        db.Users.Add(admin);
        await db.SaveChangesAsync(CancellationToken.None);

        var rows = await db.Set<OutboxEvent>().ToListAsync();
        rows.Should().Contain(r => r.EventType.Contains(nameof(UserLockedOut)))
            .Which.TenantId.Should().BeNull("platform accounts have no owning tenant");
    }
}
