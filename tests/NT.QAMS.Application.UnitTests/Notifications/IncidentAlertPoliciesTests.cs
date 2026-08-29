using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Notifications;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Notifications;

/// <summary>
/// M-06: the two safety-critical incident facts must reach the right people.
/// Recipient rule (owner decision): assignee alone when assigned; otherwise the
/// head(s) of the incident's department plus every quality manager.
/// </summary>
public class IncidentAlertPoliciesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid DeptA = Guid.CreateVersion7();
    private static readonly Guid DeptB = Guid.CreateVersion7();

    private static (AppDbContext Db, CurrentTenant Tenant) NewContext()
    {
        var tenant = new CurrentTenant();
        var clock = new FixedClock(Now);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"inc-alert-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(clock, new FakeCurrentUser()),
                new TenantStampInterceptor(tenant),
                new OutboxInterceptor(clock))
            .Options;
        return (new AppDbContext(options, tenant), tenant);
    }

    private static IncidentAlertPolicies Policy(AppDbContext db, CurrentTenant tenant) =>
        new(db, new NotificationDispatcher(
            db, tenant, new NoopEmailSender(), new FixedClock(Now),
            NullLogger<NotificationDispatcher>.Instance));

    private sealed class NoopEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct) => Task.CompletedTask;
    }

    private static UserAccount User(string email, UserRole role, Guid? department = null)
    {
        var u = UserAccount.Create(TenantId, email, email, "hash", role);
        if (department is { } d)
        {
            u.SetScope([], [d]);
        }

        return u;
    }

    private static Incident SeedIncident(AppDbContext db, Guid? department, Guid? assignee)
    {
        var incident = Incident.Report(
            "INC-2026-0001", "Wrong-site marking", "n/a", IncidentCategory.Procedural,
            HarmGrade.Severe, IntakeChannel.Web, Now.AddHours(-1), Guid.CreateVersion7());
        incident.DepartmentId = department;
        if (assignee is { } a)
        {
            incident.Triage(a, IncidentCategory.Procedural);
        }

        db.Incidents.Add(incident);
        return incident;
    }

    private async Task<List<NotificationDispatch>> DispatchesFor(AppDbContext db, Guid sourceEventId) =>
        await db.NotificationDispatches.IgnoreQueryFilters()
            .Where(d => d.SourceEventId == sourceEventId).ToListAsync();

    [Fact]
    public async Task Unassigned_escalation_notifies_department_head_and_quality_manager_only()
    {
        var (db, tenant) = NewContext();
        tenant.Set(TenantId);
        var qm = User("qm@hosp.test", UserRole.QualityManager);
        var headA = User("headA@hosp.test", UserRole.DepartmentHead, DeptA);
        var headB = User("headB@hosp.test", UserRole.DepartmentHead, DeptB);
        var analyst = User("analyst@hosp.test", UserRole.Analyst);
        db.Users.AddRange(qm, headA, headB, analyst);
        var incident = SeedIncident(db, DeptA, assignee: null);
        await db.SaveChangesAsync();
        tenant.Clear();

        var evt = new IncidentEscalated(incident.Id, incident.IncidentRef, HarmGrade.Severe);
        await Policy(db, tenant).Handle(new DomainEventNotification<IncidentEscalated>(evt), CancellationToken.None);

        var recipients = (await DispatchesFor(db, evt.EventId)).Select(d => d.RecipientUserId).ToList();
        recipients.Should().BeEquivalentTo([qm.Id, headA.Id],
            "an unassigned escalation goes to the QM and the head of the incident's department — "
            + "not the head of another department, and not an analyst");
    }

    [Fact]
    public async Task Assigned_escalation_notifies_only_the_assignee()
    {
        var (db, tenant) = NewContext();
        tenant.Set(TenantId);
        var qm = User("qm@hosp.test", UserRole.QualityManager);
        var headA = User("headA@hosp.test", UserRole.DepartmentHead, DeptA);
        var owner = User("owner@hosp.test", UserRole.Analyst);
        db.Users.AddRange(qm, headA, owner);
        var incident = SeedIncident(db, DeptA, assignee: owner.Id);
        await db.SaveChangesAsync();
        tenant.Clear();

        var evt = new IncidentEscalated(incident.Id, incident.IncidentRef, HarmGrade.Death);
        await Policy(db, tenant).Handle(new DomainEventNotification<IncidentEscalated>(evt), CancellationToken.None);

        var recipients = (await DispatchesFor(db, evt.EventId)).Select(d => d.RecipientUserId).ToList();
        recipients.Should().BeEquivalentTo([owner.Id],
            "once an incident has an owner, the alert is theirs alone");
    }

    [Fact]
    public async Task Unassigned_sentinel_notifies_department_head_and_quality_manager()
    {
        var (db, tenant) = NewContext();
        tenant.Set(TenantId);
        var qm = User("qm@hosp.test", UserRole.QualityManager);
        var headA = User("headA@hosp.test", UserRole.DepartmentHead, DeptA);
        db.Users.AddRange(qm, headA);
        var incident = SeedIncident(db, DeptA, assignee: null);
        await db.SaveChangesAsync();
        tenant.Clear();

        var evt = new SentinelDeclared(incident.Id, incident.IncidentRef, Guid.CreateVersion7());
        await Policy(db, tenant).Handle(new DomainEventNotification<SentinelDeclared>(evt), CancellationToken.None);

        var dispatches = await DispatchesFor(db, evt.EventId);
        dispatches.Select(d => d.RecipientUserId).Should().BeEquivalentTo([qm.Id, headA.Id]);
        dispatches.Should().OnlyContain(d => d.Subject.Contains("SENTINEL"));
    }

    [Fact]
    public async Task Redelivery_is_idempotent()
    {
        var (db, tenant) = NewContext();
        tenant.Set(TenantId);
        db.Users.Add(User("qm@hosp.test", UserRole.QualityManager));
        var incident = SeedIncident(db, DeptA, assignee: null);
        await db.SaveChangesAsync();
        tenant.Clear();

        var evt = new IncidentEscalated(incident.Id, incident.IncidentRef, HarmGrade.Severe);
        var policy = Policy(db, tenant);
        await policy.Handle(new DomainEventNotification<IncidentEscalated>(evt), CancellationToken.None);
        await policy.Handle(new DomainEventNotification<IncidentEscalated>(evt), CancellationToken.None);

        (await DispatchesFor(db, evt.EventId)).Should().ContainSingle("redelivery of the same event must not duplicate");
    }
}
