using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Verifies audit finding F-01 against a REAL PostgreSQL server: the FORCED
/// Row-Level-Security policies isolate tenants at the database layer, fail
/// closed, honour the controlled bypass, and reject cross-tenant writes via
/// WITH CHECK. The EF query filter is deliberately switched off in the read
/// assertions so it is RLS — not the in-process filter — that is on trial.
/// </summary>
[Collection("real-postgres")]
public sealed class RlsTenantIsolationTests(RealPostgresFixture fx)
{
    [SkippableFact]
    public async Task Rls_isolates_tenants_fails_closed_and_honours_bypass()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate(); // seed two tenants' rows past WITH CHECK
        await using var tx = await db.Database.BeginTransactionAsync();

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var a = OutlierScreening.Configure("ITEST-A", "dataset A", "u");
        var b = OutlierScreening.Configure("ITEST-B", "dataset B", "u");
        ((ITenantScoped)a).TenantId = tenantA;
        ((ITenantScoped)b).TenantId = tenantB;
        db.OutlierScreenings.AddRange(a, b);
        await db.SaveChangesAsync();

        // Drive the DB session GUCs directly (transaction-local) and read with the
        // EF filter OFF, so only PostgreSQL RLS decides what is visible.
        async Task<int> VisibleAs(Guid? tenant, bool bypass)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', {1}, true)",
                (tenant ?? Guid.Empty).ToString(), bypass ? "on" : "off");
            return await db.OutlierScreenings.IgnoreQueryFilters()
                .CountAsync(s => s.ScreeningRef == "ITEST-A" || s.ScreeningRef == "ITEST-B");
        }

        (await VisibleAs(tenantA, false)).Should().Be(1, "tenant A sees only its own row");
        (await VisibleAs(tenantB, false)).Should().Be(1, "tenant B sees only its own row");
        (await VisibleAs(null, false)).Should().Be(0, "an unresolved tenant is fail-closed");
        (await VisibleAs(tenantA, true)).Should().Be(2, "elevated infrastructure sees all tenants");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task With_check_rejects_writing_a_row_for_another_tenant()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        var actingTenant = Guid.CreateVersion7();
        ctx.Set(actingTenant); // GUC = acting tenant, NOT elevated
        await using var tx = await db.Database.BeginTransactionAsync();

        var rogue = OutlierScreening.Configure("ITEST-ROGUE", "x", "u");
        ((ITenantScoped)rogue).TenantId = Guid.CreateVersion7(); // a DIFFERENT tenant
        db.OutlierScreenings.Add(rogue);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>(
            "RLS WITH CHECK must reject inserting a row belonging to another tenant");

        await tx.RollbackAsync();
    }
}
