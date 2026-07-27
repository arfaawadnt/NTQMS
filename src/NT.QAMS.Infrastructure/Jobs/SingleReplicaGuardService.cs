using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace NT.QAMS.Infrastructure.Jobs;

/// <summary>
/// Topology sentinel (OPS-002, ADR-0001): the supported deployment is a single
/// API replica — the Outbox processor and the scheduled sweeps are not yet
/// safe to run concurrently (Phase 1 delivers <c>SKIP LOCKED</c> claiming and
/// leader election). This service holds a session-scoped PostgreSQL advisory
/// lock for the lifetime of the process; when a second instance starts against
/// the same database the lock is contended and a prominent warning is logged
/// so operators detect the unsupported scale-out immediately.
/// </summary>
public sealed class SingleReplicaGuardService(
    IConfiguration configuration,
    ILogger<SingleReplicaGuardService> logger) : BackgroundService
{
    /// <summary>
    /// Application-wide advisory lock key: ASCII "NTQMS" followed by sentinel
    /// number 01 ("single-replica"). Advisory locks are per-database, so the
    /// key only needs to be unique within NT.QMS.
    /// </summary>
    public const long SingletonLockKey = 0x4E54514D_5301;

    /// <summary>How often a contended instance re-probes for the lock.</summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return; // no database configured (test hosts) — nothing to guard
        }

        try
        {
            // The lock is session-scoped: it lives exactly as long as this
            // dedicated connection, so process death always releases it.
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(stoppingToken);

            var contendedWarningLogged = false;
            while (!await TryAcquireAsync(connection, stoppingToken))
            {
                if (!contendedWarningLogged)
                {
                    contendedWarningLogged = true;
                    logger.LogWarning(
                        "Another running instance holds the NT.QMS singleton advisory lock ({LockKey}). " +
                        "Running more than one API replica against the same database is UNSUPPORTED until the " +
                        "Phase-1 scale-out controls ship (see docs/adr/ADR-0001-single-replica-topology.md) — " +
                        "background jobs may double-process. Scale back to replicas: 1.",
                        SingletonLockKey);
                }

                await Task.Delay(RetryInterval, stoppingToken);
            }

            logger.LogInformation(
                "NT.QMS singleton advisory lock ({LockKey}) acquired — this instance is the only replica.",
                SingletonLockKey);

            // Hold the lock (keep the session alive) until shutdown.
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown — the connection disposal releases the lock
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not probe the singleton advisory lock — replica-topology enforcement is degraded. " +
                "Readiness (/health/ready) reports database availability separately.");
        }
    }

    private static async Task<bool> TryAcquireAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        command.Parameters.AddWithValue("key", SingletonLockKey);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
