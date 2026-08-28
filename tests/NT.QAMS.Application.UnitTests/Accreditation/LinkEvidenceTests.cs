using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Accreditation.Commands;
using NT.QAMS.Domain.Accreditation;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Accreditation;

/// <summary>
/// Audit finding M-15 (handler half): the polymorphic evidence reference is
/// loose by design, so the handler is the only place that can prove the source
/// record actually exists in this tenant before the link becomes counted
/// accreditation evidence.
/// </summary>
public class LinkEvidenceTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);

    private static AppDbContext NewContext(FakeCurrentTenant tenant) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"evd-link-{Guid.NewGuid()}")
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options, tenant);

    private static async Task<(AppDbContext Db, Guid SetId, Guid ElementId)> SeedSetAsync()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var db = NewContext(tenant);
        var set = StandardSet.Define(AccreditationFramework.GAHAR, "GAHAR Hospital Standards", "2026");
        var elementId = set.AddElement("PCC", "Patient-centred care", "PCC.01", "PCC.01.01", "Rights are posted", 5);
        set.TenantId = TenantId;
        db.StandardSets.Add(set);
        await db.SaveChangesAsync();
        return (db, set.Id, elementId);
    }

    private static LinkEvidenceHandler Handler(AppDbContext db) =>
        new(db, new FakeCurrentUser { UserId = Actor }, new FixedClock(Now));

    [Fact]
    public async Task Linking_to_a_missing_source_record_is_refused()
    {
        var (db, setId, elementId) = await SeedSetAsync();

        var act = () => Handler(db).Handle(new LinkEvidenceCommand(
            setId, elementId, EvidenceSourceType.Incident, Guid.CreateVersion7(),
            "INC-2026-0099", null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("EVD-004");
        (await db.EvidenceLinks.CountAsync()).Should().Be(0, "an unverifiable link must not become counted evidence");
    }

    [Fact]
    public async Task Linking_to_an_existing_in_tenant_record_succeeds()
    {
        var (db, setId, elementId) = await SeedSetAsync();
        var incident = Incident.Report(
            "INC-2026-0001", "Patient fall", "Unwitnessed fall from bed",
            IncidentCategory.Fall, HarmGrade.Moderate, IntakeChannel.Web, Now.AddDays(-1), Actor);
        incident.TenantId = TenantId;
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        var id = await Handler(db).Handle(new LinkEvidenceCommand(
            setId, elementId, EvidenceSourceType.Incident, incident.Id, "INC-2026-0001", null), CancellationToken.None);

        var link = await db.EvidenceLinks.SingleAsync();
        link.Id.Should().Be(id);
        link.SourceId.Should().Be(incident.Id);
    }

    [Fact]
    public async Task External_evidence_links_without_an_in_system_record()
    {
        var (db, setId, elementId) = await SeedSetAsync();

        await Handler(db).Handle(new LinkEvidenceCommand(
            setId, elementId, EvidenceSourceType.Other, Guid.Empty,
            "CAP accreditation certificate 2026", null), CancellationToken.None);

        (await db.EvidenceLinks.SingleAsync()).SourceId.Should().Be(Guid.Empty);
    }
}
