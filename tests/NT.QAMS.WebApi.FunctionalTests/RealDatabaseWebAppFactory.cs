using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Boots the real WebApi host against a <b>real PostgreSQL</b> database — the
/// combination the rest of this suite deliberately avoids.
/// <para>
/// Closes finding <b>VER-001</b>. Every other functional test swaps PostgreSQL
/// for the in-memory provider, where row-level security, foreign keys and CHECK
/// constraints simply do not exist. A whole class of defect is therefore
/// invisible to a green suite, and three of them escaped that way in v1.51.x:
/// RP-D1 (ledger rows unattributed to their tenant), SH-D1 (RLS on
/// <c>security_event</c> broke sign-in) and SH-D2 (a tenant foreign key broke
/// provisioning). All three were caught only by driving the live application by
/// hand. These tests drive the same HTTP surface over the same database engine,
/// so the next one fails the build instead.
/// </para>
/// <para>
/// Nothing is stubbed out here except the background jobs: the Npgsql provider,
/// the tenant-GUC connection interceptor, and the raw-SQL reference-number
/// generator are all the production ones.
/// </para>
/// </summary>
public sealed class RealDatabaseWebAppFactory : WebApplicationFactory<Program>
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=ntqams;Username=qams_app;Password=dev-only-local";

    public const string PlatformAdminEmail = "vertest-platform@test.local";
    public const string PlatformAdminPassword = "Ver-Test-Platform-1!";

    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("QMS_ITEST_POSTGRES") ?? DefaultConnection;

    public bool Available { get; }
    public string? Unavailable { get; }
    public ConcurrentQueue<string> ServerErrors { get; } = new();

    public RealDatabaseWebAppFactory()
    {
        // Belt: the same process-global variables the sibling factory uses, so
        // this host wins whichever source the minimal-hosting builder consults.
        // Braces: HostSettings() below re-applies them host-scoped. Assembly
        // parallelisation is disabled (AssemblyInfo.cs) so neither races.
        foreach (var (key, value) in HostSettings())
        {
            Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
        }

        (Available, Unavailable) = Probe(ConnectionString);
    }

    /// <summary>
    /// Host-scoped settings, applied as the LAST configuration source so they beat
    /// environment variables.
    /// <para>
    /// This matters: the sibling <see cref="QamsWebAppFactory"/> points
    /// <c>ConnectionStrings__Postgres</c> at a deliberate placeholder via a
    /// <b>process-global</b> environment variable. Both factories live in one test
    /// process, so whichever constructed last used to win — these tests failed with
    /// "password authentication failed for user \"x\"" when run alongside the rest
    /// of the suite but passed in isolation. Configuring the host rather than the
    /// process removes the race entirely.
    /// </para>
    /// </summary>
    private Dictionary<string, string?> HostSettings() => new()
    {
        ["ConnectionStrings:Postgres"] = ConnectionString,
        ["Jwt:Secret"] = "ver001-real-db-secret-key-at-least-32-chars!!",
        ["Jwt:Issuer"] = "nt-qams",
        ["Jwt:Audience"] = "nt-qams",
        ["PlatformAdmin:Email"] = PlatformAdminEmail,
        ["PlatformAdmin:Password"] = PlatformAdminPassword,
        // The schema is already migrated; these tests must never migrate it.
        ["Database:MigrateOnStartup"] = "false",
        ["RateLimit:GlobalPermitPerMinute"] = "100000",
        ["RateLimit:AuthPermitPerMinute"] = "100000",
        ["RateLimit:ESignaturePermitPerMinute"] = "100000",
        ["RateLimit:RefreshPermitPerMinute"] = "100000",
    };

    /// <summary>
    /// Confirms a server is reachable AND that the schema is the hardened one.
    /// A database without FORCE RLS would let these tests pass while proving
    /// nothing, which is the exact failure mode VER-001 describes.
    /// </summary>
    private static (bool, string?) Probe(string connectionString)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE c.relforcerowsecurity AND n.nspname IN ('qams', 'audit')";
            var forced = Convert.ToInt32(command.ExecuteScalar());
            return forced == 0
                ? (false, "PostgreSQL reachable but FORCE ROW LEVEL SECURITY is not applied — run migrations first.")
                : (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"PostgreSQL unavailable: {ex.Message}");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // Production refuses an owner-privileged role (DatabaseRoleGuard)

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(HostSettings()));

        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(new CapturingLoggerProvider(ServerErrors));
            logging.SetMinimumLevel(LogLevel.Error);
        });

        builder.ConfigureTestServices(services =>
        {
            // Background sweeps and the outbox processor would race the assertions.
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
            {
                services.Remove(descriptor);
            }
        });
    }

    /// <summary>Runs SQL as trusted infrastructure, for arranging and cleaning up test data.</summary>
    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.bypass_rls','on',false); " + sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Reads a single scalar as trusted infrastructure.</summary>
    public async Task<T?> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var bypass = new NpgsqlCommand("SELECT set_config('app.bypass_rls','on',false)", connection);
        await bypass.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, typeof(T));
    }
}
