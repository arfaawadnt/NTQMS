using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Shared connection to a REAL PostgreSQL instance for the tenant-isolation and
/// signed-record-immutability integration tests. Uses the live, already-migrated
/// schema (RLS policies + immutability triggers active) and never needs Docker.
///
/// <para>Connection string: environment variable <c>QMS_ITEST_POSTGRES</c> if set
/// (CI points this at a fresh migrated database), otherwise the local dev
/// database. If no server is reachable the fixture reports <see cref="Available"/>
/// = false and the tests skip rather than fail.</para>
///
/// <para>Every test runs inside a transaction that is rolled back, so nothing —
/// not even a signed (otherwise-immutable) row — persists between runs.</para>
/// </summary>
public sealed class RealPostgresFixture : IDisposable
{
    private const string DefaultConn =
        "Host=localhost;Port=5432;Database=ntqams;Username=qams_app;Password=dev-only-local";

    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("QMS_ITEST_POSTGRES") ?? DefaultConn;

    public bool Available { get; }
    public string? Unavailable { get; }

    public RealPostgresFixture()
    {
        try
        {
            using var probe = new NpgsqlConnection(ConnectionString);
            probe.Open();
            using var cmd = probe.CreateCommand();
            // Confirm the schema is migrated and RLS is actually forced — otherwise
            // these tests would silently prove nothing.
            cmd.CommandText =
                "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE c.relforcerowsecurity AND n.nspname = 'qams'";
            var forced = Convert.ToInt32(cmd.ExecuteScalar());
            if (forced == 0)
            {
                Available = false;
                Unavailable = "PostgreSQL reachable but FORCE ROW LEVEL SECURITY is not applied — run migrations first.";
                return;
            }

            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            Unavailable = $"PostgreSQL not reachable ({ex.GetType().Name}: {ex.Message}).";
        }
    }

    /// <summary>
    /// A fresh DbContext wired with the production interceptor pipeline, driven by
    /// the returned <see cref="TestContext"/> so the test can set the tenant and
    /// toggle elevation exactly as the app does.
    /// </summary>
    public AppDbContext CreateContext(out TestContext ctx)
    {
        ctx = new TestContext();
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new TenantConnectionInterceptor(ctx),
                new AuditStampInterceptor(clock, ctx),
                new TenantStampInterceptor(ctx),
                new FieldChangeInterceptor(clock, ctx, ctx, ctx))
            .Options;
        return new AppDbContext(options, ctx);
    }

    public void Dispose() { }
}

[CollectionDefinition("real-postgres")]
public sealed class RealPostgresCollection : ICollectionFixture<RealPostgresFixture>;
