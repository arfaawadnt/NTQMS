using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// URS-133: the new <c>tenant_mail_settings</c> table is tenant-isolated at the
/// database layer like every other tenant table — FORCE RLS fences reads to the
/// acting tenant, fails closed with no tenant context, honours the controlled
/// bypass, and WITH CHECK rejects writing another tenant's row. Real PostgreSQL,
/// EF query filter switched off so it is RLS on trial.
/// </summary>
[Collection("real-postgres")]
public sealed class MailSettingsRlsTests(RealPostgresFixture fx)
{
    [SkippableFact]
    public async Task Mail_settings_are_tenant_isolated_fail_closed_and_honour_bypass()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var a = TenantMailSettings.Create("Lab A", "a@lab-a.test", null, true, null, null);
        var b = TenantMailSettings.Create("Lab B", "b@lab-b.test", null, true, null, null);
        ((ITenantScoped)a).TenantId = tenantA;
        ((ITenantScoped)b).TenantId = tenantB;
        db.MailSettings.AddRange(a, b);
        await db.SaveChangesAsync();

        async Task<int> VisibleAs(Guid? tenant, bool bypass)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', {1}, true)",
                (tenant ?? Guid.Empty).ToString(), bypass ? "on" : "off");
            return await db.MailSettings.IgnoreQueryFilters()
                .CountAsync(m => m.FromAddress == "a@lab-a.test" || m.FromAddress == "b@lab-b.test");
        }

        (await VisibleAs(tenantA, false)).Should().Be(1, "tenant A sees only its own settings");
        (await VisibleAs(tenantB, false)).Should().Be(1, "tenant B sees only its own settings");
        (await VisibleAs(null, false)).Should().Be(0, "an unresolved tenant is fail-closed");
        (await VisibleAs(tenantA, true)).Should().Be(2, "elevated infrastructure sees all tenants");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task With_check_rejects_writing_mail_settings_for_another_tenant()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Set(Guid.CreateVersion7()); // acting tenant, NOT elevated
        await using var tx = await db.Database.BeginTransactionAsync();

        var rogue = TenantMailSettings.Create("Rogue", "rogue@lab.test", null, true, null, null);
        ((ITenantScoped)rogue).TenantId = Guid.CreateVersion7(); // a DIFFERENT tenant
        db.MailSettings.Add(rogue);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>(
            "RLS WITH CHECK must reject inserting a row belonging to another tenant");

        await tx.RollbackAsync();
    }
}
