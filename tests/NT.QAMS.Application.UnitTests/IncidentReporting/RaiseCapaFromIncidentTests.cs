using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.IncidentReporting.Commands;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace NT.QAMS.Application.UnitTests.IncidentReporting;

/// <summary>
/// The "one loop, many sources" convergence (HQMS M03): raising a CAPA/Nonconformance
/// from an incident creates a source-keyed, submitted NC, back-links it onto the
/// incident, seeds risk from the harm grade, and is idempotent under retry.
/// </summary>
public class RaiseCapaFromIncidentTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Occurred = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static AppDbContext NewContext(FakeCurrentTenant tenant) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"inc-capa-{Guid.NewGuid()}")
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options, tenant);

    private static async Task<(AppDbContext Db, FakeCurrentTenant Tenant, Incident Incident)> SeedAsync(
        HarmGrade harm = HarmGrade.Severe)
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var db = NewContext(tenant);
        var incident = Incident.Report(
            "INC-2026-0001", "Patient fall", "Unwitnessed fall from bed",
            IncidentCategory.Fall, harm, IntakeChannel.Web, Occurred, Actor);
        incident.TenantId = TenantId;
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        return (db, tenant, incident);
    }

    private static RaiseCapaFromIncidentHandler Handler(AppDbContext db, FakeCurrentTenant tenant) =>
        new(db, tenant, new FakeCurrentUser { UserId = Actor }, new FakeRefGenerator());

    [Fact]
    public async Task Creates_submitted_source_keyed_nc_and_back_links_the_incident()
    {
        var (db, tenant, incident) = await SeedAsync();

        var ncId = await Handler(db, tenant).Handle(new RaiseCapaFromIncidentCommand(incident.Id), CancellationToken.None);

        var nc = await db.Nonconformances.SingleAsync();
        nc.Id.Should().Be(ncId);
        nc.Status.Should().Be(NcStatus.Raised, "the CAPA is submitted, not left as a draft");
        nc.SourceType.Should().Be(NcSourceType.Incident);
        nc.SourceRef.Should().Be("INC:INC-2026-0001");
        nc.TenantId.Should().Be(TenantId);
        nc.RaisedBy.Should().Be(Actor);
        nc.Severity.Should().Be(4, "a severe-harm incident seeds a high-severity CAPA");

        var reloaded = await db.Incidents.SingleAsync();
        reloaded.CorrectiveActionNcId.Should().Be(ncId);
    }

    [Fact]
    public async Task Is_idempotent_second_call_returns_the_same_nc()
    {
        var (db, tenant, incident) = await SeedAsync();
        var handler = Handler(db, tenant);

        var first = await handler.Handle(new RaiseCapaFromIncidentCommand(incident.Id), CancellationToken.None);
        var second = await handler.Handle(new RaiseCapaFromIncidentCommand(incident.Id), CancellationToken.None);

        second.Should().Be(first);
        (await db.Nonconformances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Near_miss_seeds_a_low_severity_capa()
    {
        var (db, tenant, incident) = await SeedAsync(HarmGrade.NearMiss);

        await Handler(db, tenant).Handle(new RaiseCapaFromIncidentCommand(incident.Id), CancellationToken.None);

        (await db.Nonconformances.SingleAsync()).Severity.Should().Be(2);
    }
}
