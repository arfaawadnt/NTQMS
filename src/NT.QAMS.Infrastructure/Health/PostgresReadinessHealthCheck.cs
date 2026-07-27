using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace NT.QAMS.Infrastructure.Health;

/// <summary>
/// Readiness probe (OPS-008): the service is only ready to receive traffic when
/// PostgreSQL is reachable and answering queries. Exposed at
/// <c>/health/ready</c>; liveness (<c>/health/live</c>) deliberately excludes
/// this check so a database outage recycles traffic, not the process.
/// </summary>
public sealed class PostgresReadinessHealthCheck(string connectionString) : IHealthCheck
{
    /// <summary>Registration name of this check.</summary>
    public const string Name = "postgres";

    /// <summary>Tag that routes a check onto the readiness endpoint.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Ceiling on how long the probe may block a readiness request, in seconds.</summary>
    private const int ProbeTimeoutSeconds = 5;

    private readonly string _probeConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Timeout = ProbeTimeoutSeconds,
        CommandTimeout = ProbeTimeoutSeconds,
    }.ConnectionString;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_probeConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL reachable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL unreachable", ex);
        }
    }
}
