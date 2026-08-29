using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.EnvironmentOfCare;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.EnvironmentOfCare;

/// <summary>
/// M-22: an environment-of-care safety-round finding can be handed off manually
/// into the corrective-action pipeline ("one loop, many sources"). The hand-off
/// creates a source-keyed, submitted NC seeded from the finding's severity,
/// tagged with the EnvironmentOfCare source, and is idempotent under retry. Once
/// raised, the record follows the ordinary NC lifecycle. The round detail then
/// surfaces the raised NC on that finding.
/// </summary>
public class RaiseNcFromRoundFindingTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly Guid Conductor = Guid.CreateVersion7();
    private static readonly DateOnly Date = new(2026, 9, 1);

    private static AppDbContext NewContext(FakeCurrentTenant tenant) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"eoc-capa-{Guid.NewGuid()}")
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options, tenant);

    private static async Task<(AppDbContext Db, FakeCurrentTenant Tenant, SafetyRound Round, Guid FindingId)> SeedAsync(
        FindingSeverity severity = FindingSeverity.High)
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var db = NewContext(tenant);
        var round = SafetyRound.Schedule("EOR-2026-0001", "ICU", RoundType.FireSafety, Date);
        round.Start(Conductor);
        var findingId = round.AddFinding("Fire exit blocked by stored equipment", severity);
        round.TenantId = TenantId;
        db.SafetyRounds.Add(round);
        await db.SaveChangesAsync();
        return (db, tenant, round, findingId);
    }

    private static RaiseNcFromRoundFindingHandler Handler(AppDbContext db, FakeCurrentTenant tenant) =>
        new(db, tenant, new FakeCurrentUser { UserId = Actor }, new FakeRefGenerator());

    [Fact]
    public async Task Creates_submitted_source_keyed_nc_from_the_finding()
    {
        var (db, tenant, round, findingId) = await SeedAsync();

        var ncId = await Handler(db, tenant)
            .Handle(new RaiseNcFromRoundFindingCommand(round.Id, findingId), CancellationToken.None);

        var nc = await db.Nonconformances.SingleAsync();
        nc.Id.Should().Be(ncId);
        nc.Status.Should().Be(NcStatus.Raised, "the CAPA is submitted, not left as a draft");
        nc.SourceType.Should().Be(NcSourceType.EnvironmentOfCare);
        nc.SourceRef.Should().Be($"EOC:{round.RoundRef}:{findingId}");
        nc.TenantId.Should().Be(TenantId);
        nc.RaisedBy.Should().Be(Actor);
        nc.Severity.Should().Be(4, "a high-severity finding seeds a high-severity CAPA");
        nc.Title.Should().Contain(round.RoundRef).And.Contain("Fire exit blocked");
    }

    [Fact]
    public async Task Is_idempotent_second_call_returns_the_same_nc()
    {
        var (db, tenant, round, findingId) = await SeedAsync();
        var handler = Handler(db, tenant);

        var first = await handler.Handle(new RaiseNcFromRoundFindingCommand(round.Id, findingId), CancellationToken.None);
        var second = await handler.Handle(new RaiseNcFromRoundFindingCommand(round.Id, findingId), CancellationToken.None);

        second.Should().Be(first);
        (await db.Nonconformances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Critical_finding_seeds_the_highest_severity()
    {
        var (db, tenant, round, findingId) = await SeedAsync(FindingSeverity.Critical);

        await Handler(db, tenant).Handle(new RaiseNcFromRoundFindingCommand(round.Id, findingId), CancellationToken.None);

        (await db.Nonconformances.SingleAsync()).Severity.Should().Be(5);
    }

    [Fact]
    public async Task Unknown_finding_is_rejected()
    {
        var (db, tenant, round, _) = await SeedAsync();

        var act = () => Handler(db, tenant)
            .Handle(new RaiseNcFromRoundFindingCommand(round.Id, Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().Where(e => e.Code == "EOC-014");
    }

    [Fact]
    public async Task Round_detail_surfaces_the_raised_nc_on_the_finding()
    {
        var (db, tenant, round, findingId) = await SeedAsync();
        await Handler(db, tenant).Handle(new RaiseNcFromRoundFindingCommand(round.Id, findingId), CancellationToken.None);

        var detail = await new GetSafetyRoundByIdHandler(db)
            .Handle(new GetSafetyRoundByIdQuery(round.Id), CancellationToken.None);

        detail.Findings.Single(f => f.Id == findingId).RaisedNcRef
            .Should().Be("NC-2026-0001", "the detail view links the finding to the NC it spawned");
    }
}
