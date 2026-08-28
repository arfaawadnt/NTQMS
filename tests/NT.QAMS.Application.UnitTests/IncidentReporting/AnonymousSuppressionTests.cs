using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace NT.QAMS.Application.UnitTests.IncidentReporting;

/// <summary>
/// Audit finding B-01: the anonymous-reporting contract says "no reporter is
/// stored", but infrastructure re-attached the identity — the audit stamp wrote
/// <c>CreatedBy/CreatedByUserId</c> and the field-change ledger recorded the
/// actor on the "Created" row, which the incident workspace then displayed under
/// the anonymity banner. These tests pin the enforced contract: an anonymous
/// incident's CREATION persists no identity anywhere, while every later
/// transition (triage, closure) stays fully attributed to its named actor.
/// </summary>
public class AnonymousSuppressionTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Reporter = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 8, 0, 0, TimeSpan.Zero);

    private sealed class FakeChangeReason : ICurrentChangeReason
    {
        public string? Reason => null;
    }

    private static AppDbContext NewContext(FakeCurrentTenant tenant, FakeCurrentUser user, string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(
                new TenantStampInterceptor(tenant),
                new AuditStampInterceptor(new FixedClock(Now), user),
                new FieldChangeInterceptor(new FixedClock(Now), user, tenant, new FakeChangeReason()))
            .Options, tenant);

    private static Incident Anonymous() => Incident.ReportAnonymous(
        "INC-2026-0001", "Unsafe staffing on night shift", "Two nurses for 18 beds",
        IncidentCategory.Other, HarmGrade.NoHarm, IntakeChannel.Web, Now.AddHours(-2),
        new string('a', 64));

    [Fact]
    public async Task An_anonymous_incident_persists_no_reporter_identity()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var user = new FakeCurrentUser { UserId = Reporter, DisplayName = "Nadia Farouk" };
        var db = NewContext(tenant, user, $"inc-anon-{Guid.NewGuid()}");
        var incident = Anonymous();
        incident.TenantId = TenantId;

        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        incident.CreatedByUserId.Should().BeNull("the promise is that no reporter identity is stored");
        incident.CreatedBy.Should().Be("anonymous");

        var created = await db.Set<FieldChangeRecord>()
            .SingleAsync(r => r.EntityType == nameof(Incident) && r.Action == "Created");
        created.ActorId.Should().BeNull("the field-change ledger is tenant-visible");
        created.Actor.Should().Be("anonymous");
    }

    [Fact]
    public async Task An_attributed_incident_keeps_its_reporter_identity()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var user = new FakeCurrentUser { UserId = Reporter, DisplayName = "Nadia Farouk" };
        var db = NewContext(tenant, user, $"inc-anon-{Guid.NewGuid()}");
        var incident = Incident.Report(
            "INC-2026-0002", "Patient fall", "Unwitnessed fall from bed",
            IncidentCategory.Fall, HarmGrade.Minor, IntakeChannel.Web, Now.AddHours(-1), Reporter);
        incident.TenantId = TenantId;

        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        incident.CreatedByUserId.Should().Be(Reporter);
        (await db.Set<FieldChangeRecord>().SingleAsync(r => r.Action == "Created"))
            .ActorId.Should().Be(Reporter);
    }

    [Fact]
    public async Task Later_transitions_on_an_anonymous_incident_stay_attributed()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var reporter = new FakeCurrentUser { UserId = Reporter, DisplayName = "Nadia Farouk" };
        var dbName = $"inc-anon-{Guid.NewGuid()}";
        var db = NewContext(tenant, reporter, dbName);
        var incident = Anonymous();
        incident.TenantId = TenantId;
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        var triager = Guid.CreateVersion7();
        var db2 = NewContext(tenant, new FakeCurrentUser { UserId = triager, DisplayName = "QM Omar" }, dbName);
        var reloaded = await db2.Incidents.SingleAsync();
        reloaded.Triage(triager, IncidentCategory.Other);
        await db2.SaveChangesAsync();

        (await db2.Set<FieldChangeRecord>()
                .Where(r => r.Action == "Modified").ToListAsync())
            .Should().NotBeEmpty()
            .And.OnlyContain(r => r.ActorId == triager && r.Actor == "QM Omar",
                "only the anonymous CREATION suppresses identity; workflow actors are accountable");
    }
}
