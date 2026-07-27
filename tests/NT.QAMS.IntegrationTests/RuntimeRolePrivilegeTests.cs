using FluentAssertions;
using Npgsql;
using NT.QAMS.Infrastructure.Security;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Phase-0 finding TENANT-004 against a REAL PostgreSQL server: the deployment
/// safety gate must recognise an over-privileged connection role (SUPERUSER /
/// BYPASSRLS / table owner) and refuse a Production boot, because any of those
/// voids the RLS tenant-isolation and record-immutability guarantees. Also the
/// CI gate: when <c>QMS_ITEST_POSTGRES</c> is set (CI always sets it) the RLS
/// suite must actually RUN — and as a least-privilege role — instead of
/// silently skipping.
/// </summary>
[Collection("real-postgres")]
public sealed class RuntimeRolePrivilegeTests(RealPostgresFixture fx)
{
    /// <summary>CI pins the database via QMS_ITEST_POSTGRES — skipping there would hide a broken gate.</summary>
    private static bool DatabaseIsMandatory =>
        Environment.GetEnvironmentVariable("QMS_ITEST_POSTGRES") is not null;

    private void RequireDatabase()
    {
        if (!fx.Available && DatabaseIsMandatory)
        {
            Assert.Fail(
                $"QMS_ITEST_POSTGRES is set, so the RLS integration suite MUST run — {fx.Unavailable}");
        }

        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");
    }

    [SkippableFact]
    public async Task Rls_suite_runs_as_a_least_privilege_role()
    {
        RequireDatabase();

        await using var conn = new NpgsqlConnection(fx.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT r.rolsuper, r.rolbypassrls FROM pg_roles r WHERE r.rolname = current_user";
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        reader.GetBoolean(0).Should().BeFalse("RLS is not enforced for a SUPERUSER — the suite would prove nothing");
        reader.GetBoolean(1).Should().BeFalse("RLS is not enforced for a BYPASSRLS role — the suite would prove nothing");
    }

    [SkippableFact]
    public async Task Guard_findings_match_the_catalog_facts()
    {
        RequireDatabase();

        var violations = await DatabaseRoleGuard.FindViolationsAsync(fx.ConnectionString);

        await using var conn = new NpgsqlConnection(fx.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT r.rolsuper OR r.rolbypassrls,
                   (SELECT count(*) FROM pg_tables t
                     WHERE t.schemaname IN ('qams', 'audit', 'read', 'saas', 'ref')
                       AND pg_has_role(current_user, t.tableowner::regrole, 'USAGE')) > 0
            FROM pg_roles r
            WHERE r.rolname = current_user
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        var overPrivileged = reader.GetBoolean(0);
        var ownsTables = reader.GetBoolean(1);

        violations.Any(v => v.Contains("SUPERUSER") || v.Contains("BYPASSRLS"))
            .Should().Be(overPrivileged, "the guard must report exactly what the catalog says about the role");
        violations.Any(v => v.Contains("owns", StringComparison.Ordinal))
            .Should().Be(ownsTables, "the guard must report exactly what the catalog says about ownership");
    }

    [SkippableFact]
    public async Task Boot_as_owner_is_rejected()
    {
        RequireDatabase();

        // Precondition: this connection role owns the application tables (true in
        // dev and CI, where qams_app owns the schema). A fully hardened
        // environment has no violation to prove — skip rather than fabricate one.
        var violations = await DatabaseRoleGuard.FindViolationsAsync(fx.ConnectionString);
        Skip.If(violations.Count == 0,
            "The connection role is already least-privilege — nothing to reject in this environment.");

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseRoleGuard.EnsureLeastPrivilegeAsync(fx.ConnectionString));

        refusal.Message.Should().Contain("Refusing to start",
            "an over-privileged role must abort the Production boot");
        refusal.Message.Should().Contain("harden-runtime-role.sql",
            "the refusal must carry the remediation");
    }
}
