using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Tenancy.Commands;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Tenancy;

public class ProvisionTenantTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext NewContext()
    {
        var clock = new FixedClock(Now);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"provision-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(clock, new FakeCurrentUser()),
                new TenantStampInterceptor(new FakeCurrentTenant()),
                new OutboxInterceptor(clock))
            .Options;

        return new AppDbContext(options, new FakeCurrentTenant());
    }

    private static ProvisionTenantCommand Command(string identifier, string name) =>
        new(identifier, name, "admin@lab.test", "Lab Admin", "Str0ng-Initial-Pass!");

    [Fact]
    public async Task Provision_persists_tenant_admin_user_audit_stamps_and_outbox_event()
    {
        await using var db = NewContext();
        var handler = new ProvisionTenantHandler(db, new FakePasswordHasher());

        var id = await handler.Handle(
            Command("Amman-Central-Lab", "Amman Central Laboratory"),
            CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(t => t.Id == id);
        tenant.Slug.Value.Should().Be("amman-central-lab");
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.CreatedAtUtc.Should().Be(Now);
        tenant.CreatedBy.Should().Be("test-user");
        tenant.DomainEvents.Should().BeEmpty("the outbox interceptor drains events on save");

        // The transactional-outbox guarantee: the event row exists with the change.
        var outbox = await db.Set<OutboxEvent>().SingleAsync();
        outbox.EventType.Should().Contain(nameof(TenantProvisioned));
        outbox.ProcessedAtUtc.Should().BeNull();
        outbox.Payload.Should().Contain("amman-central-lab");

        // The tenant admin is created atomically with the tenant.
        var admin = await db.Users.SingleAsync(u => u.TenantId == id);
        admin.Email.Should().Be("admin@lab.test");
        admin.Role.Should().Be(Domain.IdentityAccess.UserRole.TenantAdmin);
        admin.PasswordHash.Should().StartWith("hashed:");
    }

    [Fact]
    public async Task Provision_rejects_duplicate_identifier()
    {
        await using var db = NewContext();
        var handler = new ProvisionTenantHandler(db, new FakePasswordHasher());

        await handler.Handle(Command("acme-lab", "Acme 1"), CancellationToken.None);

        var duplicate = () => handler.Handle(Command("ACME-LAB", "Acme 2"), CancellationToken.None);

        (await duplicate.Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("TENANT-005");
    }
}
