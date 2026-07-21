using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.AuditManagement.Policies;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.AuditManagement;

/// <summary>
/// Proves the cross-module saga end-to-end on the real DbContext + interceptors:
/// FindingRaised → NC created (source-keyed) → finding acknowledged — and that
/// at-least-once redelivery does not duplicate the NC.
/// </summary>
public class FindingToNcPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Auditor = Guid.CreateVersion7();

    private static (AppDbContext Db, CurrentTenant Tenant) NewContext()
    {
        var tenant = new CurrentTenant();
        var clock = new FixedClock(Now);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"saga-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(clock, new FakeCurrentUser()),
                new TenantStampInterceptor(tenant),
                new OutboxInterceptor(clock))
            .Options;
        return (new AppDbContext(options, tenant), tenant);
    }

    private static async Task<(AppDbContext Db, CurrentTenant Tenant, FindingRaised Event)> SeedAsync()
    {
        var (db, tenant) = NewContext();
        tenant.Set(TenantId);

        var audit = Audit.Schedule("AUD-2026-0001", "Q3 audit", AuditType.Internal, Auditor, new DateOnly(2026, 9, 1));
        audit.AddChecklistItem("7.2", "Methods validated?");
        audit.Start();
        var findingId = audit.RaiseFinding(FindingGrade.MajorNc, "Method X unvalidated", Auditor);
        db.Audits.Add(audit);
        await db.SaveChangesAsync();

        var evt = new FindingRaised(
            audit.Id, "AUD-2026-0001", findingId, FindingGrade.MajorNc,
            "Method X unvalidated", TenantId, Auditor);

        // Simulate the background scope: no ambient tenant until the policy sets it.
        tenant.Clear();
        return (db, tenant, evt);
    }

    private static FindingToNcPolicy Policy(AppDbContext db, CurrentTenant tenant) =>
        new(db, tenant, new FakeRefGenerator(), NullLogger<FindingToNcPolicy>.Instance);

    [Fact]
    public async Task Nc_graded_finding_creates_submitted_nc_and_acknowledges_finding()
    {
        var (db, tenant, evt) = await SeedAsync();

        await Policy(db, tenant).Handle(new DomainEventNotification<FindingRaised>(evt), CancellationToken.None);

        var nc = await db.Nonconformances.SingleAsync();
        nc.Status.Should().Be(NcStatus.Raised);
        nc.SourceType.Should().Be(NcSourceType.Audit);
        nc.SourceRef.Should().Be($"AUD-2026-0001#{evt.FindingId:N}");
        nc.TenantId.Should().Be(TenantId);
        nc.RaisedBy.Should().Be(Auditor);
        nc.Severity.Should().Be(4, "major findings map to high severity");

        var audit = await db.Audits.Include(a => a.Findings).SingleAsync();
        audit.Findings.Single().NcId.Should().Be(nc.Id);
    }

    [Fact]
    public async Task Redelivery_is_idempotent_no_duplicate_nc()
    {
        var (db, tenant, evt) = await SeedAsync();
        var policy = Policy(db, tenant);
        var notification = new DomainEventNotification<FindingRaised>(evt);

        await policy.Handle(notification, CancellationToken.None);
        await policy.Handle(notification, CancellationToken.None); // at-least-once redelivery

        (await db.Nonconformances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Ofi_findings_are_ignored()
    {
        var (db, tenant, evt) = await SeedAsync();
        var ofi = evt with { Grade = FindingGrade.Ofi };

        await Policy(db, tenant).Handle(new DomainEventNotification<FindingRaised>(ofi), CancellationToken.None);

        (await db.Nonconformances.AnyAsync()).Should().BeFalse();
    }
}
