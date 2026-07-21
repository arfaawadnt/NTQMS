using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Notifications;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Notifications;

public class NotificationDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            if (Fail)
            {
                throw new InvalidOperationException("SMTP unreachable");
            }

            Sent.Add((to, subject));
            return Task.CompletedTask;
        }
    }

    private static (AppDbContext Db, CurrentTenant Tenant) NewContext()
    {
        var tenant = new CurrentTenant();
        var clock = new FixedClock(Now);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ntf-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(clock, new FakeCurrentUser()),
                new TenantStampInterceptor(tenant),
                new OutboxInterceptor(clock))
            .Options;
        return (new AppDbContext(options, tenant), tenant);
    }

    private static async Task SeedAsync(AppDbContext db, CurrentTenant tenant)
    {
        tenant.Set(TenantId);

        var qm = UserAccount.Create(TenantId, "qm@lab.test", "QM", "hash", UserRole.QualityManager);
        var analyst = UserAccount.Create(TenantId, "analyst@lab.test", "Analyst", "hash", UserRole.Analyst);
        db.Users.AddRange(qm, analyst);

        var rule = NotificationRule.Create(
            "NC_RAISED", "QualityManager", emailEnabled: true,
            "NC raised: {ref}", "Nonconformance {ref} severity {severity} awaits triage.");
        db.NotificationRules.Add(rule);
        await db.SaveChangesAsync();
        tenant.Clear(); // Simulate the background scope.
    }

    [Fact]
    public async Task Dispatch_matches_rule_resolves_roles_renders_templates_and_emails()
    {
        var (db, tenant) = NewContext();
        await SeedAsync(db, tenant);
        var email = new CapturingEmailSender();
        var dispatcher = new NotificationDispatcher(
            db, tenant, email, new FixedClock(Now), NullLogger<NotificationDispatcher>.Instance);

        var eventId = Guid.CreateVersion7();
        await dispatcher.DispatchAsync(eventId, TenantId, "NC_RAISED",
            new Dictionary<string, string> { ["ref"] = "NC-2026-0007", ["severity"] = "4" },
            CancellationToken.None);

        var dispatches = await db.NotificationDispatches.IgnoreQueryFilters().ToListAsync();
        dispatches.Should().ContainSingle("only the QualityManager matches the rule's roles");

        var d = dispatches.Single();
        d.Subject.Should().Be("NC raised: NC-2026-0007");
        d.Body.Should().Contain("severity 4");
        d.EmailStatus.Should().Be(DispatchStatus.Sent);
        d.TenantId.Should().Be(TenantId);
        email.Sent.Should().ContainSingle().Which.To.Should().Be("qm@lab.test");
    }

    [Fact]
    public async Task Redelivery_of_same_event_is_a_no_op()
    {
        var (db, tenant) = NewContext();
        await SeedAsync(db, tenant);
        var dispatcher = new NotificationDispatcher(
            db, tenant, new CapturingEmailSender(), new FixedClock(Now),
            NullLogger<NotificationDispatcher>.Instance);

        var eventId = Guid.CreateVersion7();
        var context = new Dictionary<string, string> { ["ref"] = "NC-1", ["severity"] = "2" };

        await dispatcher.DispatchAsync(eventId, TenantId, "NC_RAISED", context, CancellationToken.None);
        await dispatcher.DispatchAsync(eventId, TenantId, "NC_RAISED", context, CancellationToken.None);

        (await db.NotificationDispatches.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Email_failure_records_error_but_keeps_the_inapp_notification()
    {
        var (db, tenant) = NewContext();
        await SeedAsync(db, tenant);
        var dispatcher = new NotificationDispatcher(
            db, tenant, new CapturingEmailSender { Fail = true }, new FixedClock(Now),
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchAsync(Guid.CreateVersion7(), TenantId, "NC_RAISED",
            new Dictionary<string, string> { ["ref"] = "NC-2", ["severity"] = "3" },
            CancellationToken.None);

        var d = await db.NotificationDispatches.IgnoreQueryFilters().SingleAsync();
        d.EmailStatus.Should().Be(DispatchStatus.Failed);
        d.Error.Should().Contain("SMTP unreachable");
        d.Subject.Should().Contain("NC-2", "the in-app feed row survives email failure");
    }

    [Fact]
    public async Task Unmatched_event_key_dispatches_nothing()
    {
        var (db, tenant) = NewContext();
        await SeedAsync(db, tenant);
        var dispatcher = new NotificationDispatcher(
            db, tenant, new CapturingEmailSender(), new FixedClock(Now),
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchAsync(Guid.CreateVersion7(), TenantId, "UNKNOWN_EVENT",
            new Dictionary<string, string>(), CancellationToken.None);

        (await db.NotificationDispatches.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }
}
