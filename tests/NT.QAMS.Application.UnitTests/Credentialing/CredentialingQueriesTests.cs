using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Credentialing;
using NT.QAMS.Domain.Credentialing;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Credentialing;

/// <summary>
/// The M13 read side: the tiered licence-expiry register and the point-of-care privilege check,
/// both computed relative to "today" from the clock.
/// </summary>
public class CredentialingQueriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 1);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Verifier = Guid.CreateVersion7();

    private static AppDbContext NewContext()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"credentialing-{Guid.NewGuid()}")
                .AddInterceptors(new TenantStampInterceptor(tenant))
                .Options, tenant);
    }

    private static Practitioner Seed(AppDbContext db)
    {
        var p = Practitioner.Register("PRC-1", "Dr Alice Roe", "Cardiology");
        // Three licences: expired, critical (≤30d), warning (≤90d).
        p.AddLicence(CredentialType.MedicalLicence, "ML-1", "Council", Today.AddDays(-5));
        p.AddLicence(CredentialType.Bls, "BLS-1", "AHA", Today.AddDays(20));
        p.AddLicence(CredentialType.Acls, "ACLS-1", "AHA", Today.AddDays(60));
        var verified = p.AddLicence(CredentialType.BoardCertification, "BC-1", "Board", Today.AddYears(1));
        p.VerifyLicence(verified, Verifier, "Board register", Now);
        var priv = p.RequestPrivilege("Coronary angiography");
        p.GrantPrivilege(priv, Today.AddYears(1));
        p.Credential(Today.AddYears(1));
        p.TenantId = TenantId;
        db.Practitioners.Add(p);
        return p;
    }

    [Fact]
    public async Task Expiry_register_tiers_licences_by_days_to_expiry()
    {
        var db = NewContext();
        Seed(db);
        await db.SaveChangesAsync();

        var rows = await new GetExpiringCredentialsHandler(db, new FixedClock(Now))
            .Handle(new GetExpiringCredentialsQuery(WithinDays: 90), CancellationToken.None);

        // The board certification (1 year out) is beyond the 90-day cutoff and excluded.
        rows.Select(r => r.Tier).Should().BeEquivalentTo(new[] { "Expired", "Critical", "Warning" });
        rows.First().Tier.Should().Be("Expired", "results are ordered by ascending days-to-expiry");
        rows.Single(r => r.Type == "Bls").Tier.Should().Be("Critical");
        rows.Single(r => r.Type == "Acls").Tier.Should().Be("Warning");
    }

    [Fact]
    public async Task Point_of_care_check_confirms_an_active_privilege()
    {
        var db = NewContext();
        var p = Seed(db);
        await db.SaveChangesAsync();

        var handler = new VerifyPrivilegeHandler(db, new FixedClock(Now));

        var held = await handler.Handle(new VerifyPrivilegeQuery(p.Id, "Coronary angiography"), CancellationToken.None);
        held.Holds.Should().BeTrue();

        var absent = await handler.Handle(new VerifyPrivilegeQuery(p.Id, "Neurosurgery"), CancellationToken.None);
        absent.Holds.Should().BeFalse();
        absent.Detail.Should().NotBeNull();
    }
}
