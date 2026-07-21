using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.AnalyticalQuality;

public class PtToNcPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Analyst = Guid.CreateVersion7();

    private static (AppDbContext Db, CurrentTenant Tenant) NewContext()
    {
        var tenant = new CurrentTenant();
        var clock = new FixedClock(Now);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pt-nc-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(clock, new FakeCurrentUser()),
                new TenantStampInterceptor(tenant),
                new OutboxInterceptor(clock))
            .Options;
        return (new AppDbContext(options, tenant), tenant);
    }

    private static PtUnsatisfactory Event() =>
        new(Guid.CreateVersion7(), "PT-2026-0001", "Glucose", 3.4m, TenantId, Analyst);

    private static PtToNcPolicy Policy(AppDbContext db, CurrentTenant tenant) =>
        new(db, tenant, new FakeRefGenerator(), NullLogger<PtToNcPolicy>.Instance);

    [Fact]
    public async Task Unsatisfactory_pt_raises_submitted_nc_with_source_ref()
    {
        var (db, tenant) = NewContext();
        var e = Event();

        await Policy(db, tenant).Handle(new DomainEventNotification<PtUnsatisfactory>(e), CancellationToken.None);

        var nc = await db.Nonconformances.IgnoreQueryFilters().SingleAsync();
        nc.Status.Should().Be(NcStatus.Raised);
        nc.SourceType.Should().Be(NcSourceType.ProficiencyTest);
        nc.SourceRef.Should().Be("PT:PT-2026-0001");
        nc.TenantId.Should().Be(TenantId);
        nc.Severity.Should().Be(4);
    }

    [Fact]
    public async Task Redelivery_is_idempotent()
    {
        var (db, tenant) = NewContext();
        var policy = Policy(db, tenant);
        var n = new DomainEventNotification<PtUnsatisfactory>(Event());

        await policy.Handle(n, CancellationToken.None);
        await policy.Handle(n, CancellationToken.None);

        (await db.Nonconformances.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }
}
