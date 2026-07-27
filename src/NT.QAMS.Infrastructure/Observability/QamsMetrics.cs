using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace NT.QAMS.Infrastructure.Observability;

/// <summary>
/// OBS-003: the application's own instruments (System.Diagnostics.Metrics),
/// published on the <c>/metrics</c> endpoint next to the built-in ASP.NET Core
/// RED metrics and the Npgsql pool meter. Everything here feeds an actionable
/// alert (see deploy/OBSERVABILITY.md): dead-letters &gt; 0, outbox backlog
/// age, and job-liveness (time since last successful run).
/// </summary>
public static class QamsMetrics
{
    /// <summary>Meter name the metrics provider subscribes to.</summary>
    public const string MeterName = "NT.QAMS";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Outbox events published successfully.</summary>
    public static readonly Counter<long> OutboxProcessed =
        Meter.CreateCounter<long>("qams.outbox.processed", "{event}", "Outbox events published successfully");

    /// <summary>Outbox delivery attempts that failed (will retry with backoff).</summary>
    public static readonly Counter<long> OutboxFailed =
        Meter.CreateCounter<long>("qams.outbox.failed", "{event}", "Outbox delivery attempts that failed");

    /// <summary>Outbox events moved to the dead-letter state (alert: any increase).</summary>
    public static readonly Counter<long> OutboxDeadLettered =
        Meter.CreateCounter<long>("qams.outbox.dead_lettered", "{event}", "Outbox events dead-lettered after MaxAttempts");

    private static long _outboxBacklog;
    private static long _outboxDeadLetters;
    private static double _outboxOldestPendingAgeSeconds;
    private static readonly ConcurrentDictionary<string, double> JobLastSuccessUnixSeconds = new();

    static QamsMetrics()
    {
        Meter.CreateObservableGauge(
            "qams.outbox.backlog", () => Volatile.Read(ref _outboxBacklog),
            "{event}", "Unprocessed (live) outbox rows");
        Meter.CreateObservableGauge(
            "qams.outbox.dead_letters", () => Volatile.Read(ref _outboxDeadLetters),
            "{event}", "Outbox rows currently in the dead-letter state");
        Meter.CreateObservableGauge(
            "qams.outbox.oldest_pending_age_seconds", () => Volatile.Read(ref _outboxOldestPendingAgeSeconds),
            "s", "Age of the oldest unprocessed outbox row");
        Meter.CreateObservableGauge(
            "qams.job.last_success_timestamp_seconds",
            () => JobLastSuccessUnixSeconds.Select(pair =>
                new Measurement<double>(pair.Value, new KeyValuePair<string, object?>("job", pair.Key))),
            "s", "Unix time of each recurring job's last successful run (liveness)");
    }

    /// <summary>Refreshes the outbox queue gauges (called by the processor's stats poll).</summary>
    public static void RecordOutboxQueueStats(long backlog, long deadLetters, double oldestPendingAgeSeconds)
    {
        Volatile.Write(ref _outboxBacklog, backlog);
        Volatile.Write(ref _outboxDeadLetters, deadLetters);
        Volatile.Write(ref _outboxOldestPendingAgeSeconds, oldestPendingAgeSeconds);
    }

    /// <summary>Marks a recurring job's successful completion (liveness alert source).</summary>
    public static void RecordJobSuccess(string jobName, DateTimeOffset completedAt) =>
        JobLastSuccessUnixSeconds[jobName] = completedAt.ToUnixTimeSeconds();
}
