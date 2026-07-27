using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using NT.QAMS.Infrastructure.Health;
using NT.QAMS.Infrastructure.Jobs;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Phase-0 findings OPS-008 and OPS-002 against a REAL PostgreSQL server: the
/// DB-backed readiness probe must report Healthy exactly when PostgreSQL
/// answers, and the single-replica advisory lock must be contended for a
/// second instance (the sentinel's start-up warning path).
/// </summary>
[Collection("real-postgres")]
public sealed class ReadinessAndTopologyTests(RealPostgresFixture fx)
{
    /// <summary>TCP port 9 (discard) — never a PostgreSQL server, refuses immediately.</summary>
    private const string UnreachableConnectionString =
        "Host=localhost;Port=9;Database=none;Username=none;Password=none;Timeout=1";

    [SkippableFact]
    public async Task Readiness_is_healthy_when_postgres_answers()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        var check = new PostgresReadinessHealthCheck(fx.ConnectionString);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Readiness_is_unhealthy_when_postgres_is_unreachable()
    {
        var check = new PostgresReadinessHealthCheck(UnreachableConnectionString);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy,
            "a service that cannot reach its database must not receive traffic");
        result.Exception.Should().NotBeNull("the probe surfaces the connection failure for diagnostics");
    }

    [SkippableFact]
    public async Task Singleton_advisory_lock_is_contended_for_a_second_instance()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        await using var first = new NpgsqlConnection(fx.ConnectionString);
        await using var second = new NpgsqlConnection(fx.ConnectionString);
        await first.OpenAsync();
        await second.OpenAsync();

        var firstAcquired = await TryLockAsync(first);
        try
        {
            var secondAcquired = await TryLockAsync(second);

            // Whether "first" got the lock or an already-running API instance
            // holds it, a concurrent second claimant must always be refused —
            // that refusal is what triggers the sentinel's scale-out warning.
            (firstAcquired && secondAcquired).Should().BeFalse(
                "two sessions must never hold the singleton lock simultaneously");
            secondAcquired.Should().BeFalse(
                "the lock was already held by this test or by a running instance");
        }
        finally
        {
            if (firstAcquired)
            {
                await using var unlock = first.CreateCommand();
                unlock.CommandText = "SELECT pg_advisory_unlock(@key)";
                unlock.Parameters.AddWithValue("key", SingleReplicaGuardService.SingletonLockKey);
                await unlock.ExecuteScalarAsync();
            }
        }
    }

    private static async Task<bool> TryLockAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        command.Parameters.AddWithValue("key", SingleReplicaGuardService.SingletonLockKey);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
