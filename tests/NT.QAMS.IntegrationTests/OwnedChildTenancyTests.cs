using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Schema hardening Phase 4. Before this migration a direct
/// <c>SELECT * FROM qams.rca_record</c> returned every tenant's rows: the owned
/// child tables carried no <c>tenant_id</c>, so RLS had nothing to fence on and
/// the CASCADE FK protected deletion integrity only — never read isolation.
/// <para>
/// These tests prove the three guarantees the phase claims, against real
/// PostgreSQL: children are tenant-isolated on read, fail closed with no tenant
/// context, and cannot be written with a tenant that differs from their
/// parent's (the composite FK makes drift structurally impossible rather than
/// merely detected).
/// </para>
/// </summary>
[Collection("real-postgres")]
public sealed class OwnedChildTenancyTests(RealPostgresFixture fx)
{
    private const string ForeignKeyViolation = "23503";

    /// <summary>
    /// One child per aggregate family, so a regression in any one branch of the
    /// 30-table sweep shows up: improvement, documents, audit, equipment, risk
    /// and the v1.51 authorization tables.
    /// </summary>
    public static TheoryData<string, string, string> Children => new()
    {
        { "qams.capa_action", "qams.nonconformance", "nc_id" },
        { "qams.rca_record", "qams.nonconformance", "nc_id" },
        { "qams.document_version", "qams.controlled_document", "document_id" },
        { "qams.audit_finding", "qams.audit", "audit_id" },
        { "qams.calibration_record", "qams.equipment_item", "equipment_id" },
        { "qams.mitigation_action", "qams.risk_item", "risk_id" },
        { "qams.role_permission", "qams.role", "role_id" },
    };

    [SkippableTheory]
    [MemberData(nameof(Children))]
    public async Task Child_rows_are_visible_only_to_their_own_tenant(string child, string parent, string fk)
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        async Task<long> CountAs(string tenantGuc)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', 'off', true)",
                tenantGuc);
            return await db.Database
                .SqlQueryRaw<long>($"SELECT count(*) AS \"Value\" FROM {child}")
                .SingleAsync();
        }

        await db.Database.ExecuteSqlRawAsync("SELECT set_config('app.bypass_rls', 'on', true)");
        var total = await db.Database
            .SqlQueryRaw<long>($"SELECT count(*) AS \"Value\" FROM {child}")
            .SingleAsync();
        var tenants = await db.Database
            .SqlQueryRaw<Guid>($"SELECT DISTINCT tenant_id AS \"Value\" FROM {child}")
            .ToListAsync();

        Skip.If(tenants.Count == 0, $"{child} has no rows in this database");

        long perTenantSum = 0;
        foreach (var tenant in tenants)
        {
            perTenantSum += await CountAs(tenant.ToString());
        }

        perTenantSum.Should().Be(total,
            "each row must be visible to exactly one tenant - no row seen twice, none invisible");
        (await CountAs(Guid.Empty.ToString())).Should().Be(0, "an unresolved tenant is fail-closed");

        // And every visible row genuinely belongs to the asking tenant.
        var first = tenants[0];
        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', 'off', true)",
            first.ToString());
        (await db.Database
                .SqlQueryRaw<long>($"SELECT count(*) AS \"Value\" FROM {child} WHERE tenant_id <> '{first}'")
                .SingleAsync())
            .Should().Be(0, "no foreign-tenant row may be visible");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task A_child_cannot_be_written_with_a_tenant_that_differs_from_its_parent()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync("SELECT set_config('app.bypass_rls', 'on', true)");
        var nc = await db.Database
            .SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM qams.nonconformance LIMIT 1")
            .ToListAsync();
        Skip.If(nc.Count == 0, "no nonconformance rows to hang a child from");

        await db.Database.ExecuteSqlRawAsync("SAVEPOINT drift");
        var drift = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO qams.capa_action (id, nc_id, tenant_id, type, details, owner_id, due_date, status) " +
            $"VALUES (gen_random_uuid(), '{nc[0]}', gen_random_uuid(), 'Corrective', 'drift', gen_random_uuid(), '2026-12-31', 'Open')");
        (await Assert.ThrowsAsync<PostgresException>(drift)).SqlState.Should().Be(ForeignKeyViolation,
            "the composite FK makes a child whose tenant differs from its parent's impossible");
        await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT drift");

        // Control: the same insert with the parent's own tenant is accepted, so
        // the constraint is discriminating rather than simply blocking writes.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO qams.capa_action (id, nc_id, tenant_id, type, details, owner_id, due_date, status) " +
            $"SELECT gen_random_uuid(), n.id, n.tenant_id, 'Corrective', 'control', gen_random_uuid(), '2026-12-31', 'Open' " +
            $"FROM qams.nonconformance n WHERE n.id = '{nc[0]}'");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Every_owned_child_table_carries_tenant_id_and_full_rls()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out _);

        // Structural assertion over the whole sweep, so a table added later
        // without the treatment fails here rather than leaking silently.
        var unprotected = await db.Database.SqlQueryRaw<string>("""
            SELECT n.nspname || '.' || c.relname AS "Value"
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r'
              AND n.nspname IN ('qams', 'read')
              AND EXISTS (SELECT 1 FROM information_schema.columns col
                          WHERE col.table_schema = n.nspname AND col.table_name = c.relname
                            AND col.column_name = 'tenant_id' AND col.is_nullable = 'NO')
              AND (NOT c.relrowsecurity
                   OR NOT c.relforcerowsecurity
                   OR NOT EXISTS (SELECT 1 FROM pg_policies p
                                  WHERE p.schemaname = n.nspname AND p.tablename = c.relname
                                    AND p.policyname = 'tenant_isolation'))
            """).ToListAsync();

        unprotected.Should().BeEmpty(
            "every table with a NOT NULL tenant_id must have RLS enabled, forced, and a tenant_isolation policy");
    }
}
