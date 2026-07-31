using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Authorization;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure.Persistence;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Authorization;

/// <summary>
/// The seeded role matrix is the upgrade contract: on the day configurable
/// privileges ship, each fixed tier's reach is reproduced by its seeded role.
/// These pins hold the sensitive cells of that contract — the ones whose drift
/// would silently widen or narrow access.
/// </summary>
public class SystemRoleCatalogTests
{
    private static AppDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"roles-{Guid.NewGuid()}")
            .Options,
        new FakeCurrentTenant());

    private static async Task<Dictionary<string, Role>> SeedAsync(AppDbContext db, Guid tenantId)
    {
        await SystemRoleCatalog.SeedMissingAsync(db, tenantId, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return await db.Roles.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .ToDictionaryAsync(r => r.Name, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Seeding_creates_the_five_system_roles_and_is_idempotent()
    {
        await using var db = NewContext();
        var tenantId = Guid.CreateVersion7();

        var roles = await SeedAsync(db, tenantId);
        roles.Should().HaveCount(5);
        roles.Values.Should().OnlyContain(r => r.IsSystem && r.IsActive);

        var again = await SystemRoleCatalog.SeedMissingAsync(db, tenantId, CancellationToken.None);
        again.Should().BeEmpty("re-seeding must never touch existing roles");
    }

    [Fact]
    public async Task Every_seeded_grant_is_a_known_catalogue_key()
    {
        await using var db = NewContext();
        var roles = await SeedAsync(db, Guid.CreateVersion7());

        foreach (var role in roles.Values)
        {
            role.PermissionKeys.Should().OnlyContain(k => PermissionCatalog.IsKnown(k));
        }
    }

    [Fact]
    public async Task Only_the_tenant_administrator_manages_roles_and_users()
    {
        await using var db = NewContext();
        var roles = await SeedAsync(db, Guid.CreateVersion7());

        roles[SystemRoleCatalog.TenantAdministrator].Grants(PermissionCatalog.ManageRoles).Should().BeTrue();
        roles[SystemRoleCatalog.TenantAdministrator]
            .Grants(PermissionCatalog.Key(PermissionCatalog.Users, PermissionAction.Manage)).Should().BeTrue();

        foreach (var name in new[]
        {
            SystemRoleCatalog.QualityManager, SystemRoleCatalog.DepartmentHead,
            SystemRoleCatalog.Analyst, SystemRoleCatalog.ExternalAuditor,
        })
        {
            roles[name].Grants(PermissionCatalog.ManageRoles).Should().BeFalse(name);
            roles[name].PermissionKeys.Should().NotContain(
                k => k.StartsWith(PermissionCatalog.Users + ".", StringComparison.Ordinal), name);
            roles[name].PermissionKeys.Should().NotContain(
                k => k.StartsWith(PermissionCatalog.TenantSettings + ".", StringComparison.Ordinal), name);
        }
    }

    [Fact]
    public async Task The_external_auditor_holds_no_write_privileges_at_all()
    {
        await using var db = NewContext();
        var roles = await SeedAsync(db, Guid.CreateVersion7());

        var writes = new[]
        {
            PermissionAction.Create, PermissionAction.Edit, PermissionAction.Approve,
            PermissionAction.Void, PermissionAction.Sign, PermissionAction.Manage,
        }.Select(a => "." + a.ToString().ToLowerInvariant()).ToArray();

        roles[SystemRoleCatalog.ExternalAuditor].PermissionKeys
            .Should().OnlyContain(k => !writes.Any(w => k.EndsWith(w, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Parity_pins_for_gates_that_were_stricter_than_their_module_default()
    {
        await using var db = NewContext();
        var roles = await SeedAsync(db, Guid.CreateVersion7());
        var auditor = roles[SystemRoleCatalog.ExternalAuditor];
        var deptHead = roles[SystemRoleCatalog.DepartmentHead];
        var analyst = roles[SystemRoleCatalog.Analyst];
        var qm = roles[SystemRoleCatalog.QualityManager];

        // Review packs were exportable by QM/admin only.
        auditor.Grants(PermissionCatalog.Key(PermissionCatalog.ManagementReviews, PermissionAction.Export))
            .Should().BeFalse();
        // The audit trail and signature manifest remain auditor-readable.
        auditor.Grants(PermissionCatalog.Key(PermissionCatalog.Compliance, PermissionAction.Export))
            .Should().BeTrue();

        // Scheduling audits and management reviews, and drafting policy, was QM+.
        deptHead.Grants(PermissionCatalog.Key(PermissionCatalog.Audits, PermissionAction.Create)).Should().BeFalse();
        deptHead.Grants(PermissionCatalog.Key(PermissionCatalog.ManagementReviews, PermissionAction.Create)).Should().BeFalse();
        deptHead.PermissionKeys.Should().NotContain(
            k => k.StartsWith(PermissionCatalog.QualityPolicy + ".", StringComparison.Ordinal));

        // Department heads run their department's records.
        deptHead.Grants(PermissionCatalog.Key(PermissionCatalog.Competencies, PermissionAction.Create)).Should().BeTrue();
        deptHead.Grants(PermissionCatalog.Key(PermissionCatalog.Documents, PermissionAction.Approve)).Should().BeTrue();

        // Analysts record work but hold none of the gated write surfaces.
        analyst.Grants(PermissionCatalog.Key(PermissionCatalog.Complaints, PermissionAction.Edit)).Should().BeFalse();
        analyst.Grants(PermissionCatalog.Key(PermissionCatalog.Tasks, PermissionAction.Create)).Should().BeFalse();
        analyst.Grants(PermissionCatalog.Key(PermissionCatalog.Nonconformances, PermissionAction.Create)).Should().BeTrue();

        // QM maintains organisation structure but cannot deactivate org units.
        qm.Grants(PermissionCatalog.Key(PermissionCatalog.Organization, PermissionAction.Create)).Should().BeTrue();
        qm.Grants(PermissionCatalog.Key(PermissionCatalog.Organization, PermissionAction.Manage)).Should().BeFalse();
        qm.Grants(PermissionCatalog.Key(PermissionCatalog.RolesPrivileges, PermissionAction.View)).Should().BeTrue();
    }

    [Fact]
    public void Every_fixed_tier_maps_to_a_seeded_role_name()
    {
        foreach (var tier in Enum.GetValues<UserRole>())
        {
            SystemRoleCatalog.RoleNameFor(tier).Should().NotBeNullOrWhiteSpace();
        }
    }
}
