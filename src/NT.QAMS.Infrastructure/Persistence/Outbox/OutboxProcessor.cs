using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Persistence.Outbox;

/// <summary>Typed outbox configuration (Outbox:* keys), validated at composition.</summary>
/// <param name="RetentionDays">How long processed rows are kept before the purge deletes them (MSG-007).</param>
public sealed record OutboxOptions(int RetentionDays)
{
    public OutboxOptions Validated() =>
        RetentionDays > 0 ? this : throw new InvalidOperationException(
            "Outbox:RetentionDays must be a positive number of days.");
}

/// <summary>
/// Claims due outbox rows and publishes them in-process. At-least-once:
/// a crash between publish and mark re-delivers; consumers are idempotent by
/// natural key or EventId. Robustness (Phase 1):
/// <list type="bullet">
/// <item>rows are claimed with FOR UPDATE SKIP LOCKED under a lease, so
/// concurrent processors never publish the same row twice (OPS-002);</item>
/// <item>failures retry on per-event exponential backoff with jitter
/// (MSG-005) — a failing event never head-of-line-blocks healthy ones;</item>
/// <item>after MaxAttempts the row dead-letters with an ERROR-level alert log
/// carrying the event id/type for triage (MSG-004);</item>
/// <item>processed rows are purged after the retention window (MSG-007).</item>
/// </list>
/// </summary>
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    OutboxOptions options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 5;

    /// <summary>How long a claim protects a row before a crashed claimant's rows are reclaimable.</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);

    /// <summary>First-retry delay; doubles per attempt (5s, 10s, 20s, 40s), plus up to 25% jitter.</summary>
    public static readonly TimeSpan BackoffBase = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextPurgeDueAt = clock.UtcNow; // first purge on startup, then hourly

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);

                if (clock.UtcNow >= nextPurgeDueAt)
                {
                    nextPurgeDueAt = clock.UtcNow + PurgeInterval;
                    await RunRetentionPurgeAsync(stoppingToken);
                }

                if (processed == 0)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSweepFailed(logger, ex);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        // The outbox chains audit-trail rows for many tenants in one SaveChanges;
        // elevate so RLS bypass applies to this trusted infrastructure batch.
        scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Elevate();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var ledger = new Compliance.AuditTrailAppender(db);

        var now = clock.UtcNow;
        var batch = await ClaimDueBatchAsync(db, now, cancellationToken);

        foreach (var row in batch)
        {
            try
            {
                var notification = Deserialize(row);
                await publisher.Publish(notification, cancellationToken);
                // Tamper-evident trail: every processed event is chained into the ledger
                // in the same SaveChanges as marking the row processed.
                await ledger.AppendAsync(
                    row.TenantId, row.Id, row.EventType, row.Payload, row.OccurredAtUtc, cancellationToken);
                row.ProcessedAtUtc = clock.UtcNow;
                row.ClaimedUntilUtc = null;
            }
            catch (Exception ex)
            {
                row.Attempts++;
                row.LastError = ex.Message;
                row.ClaimedUntilUtc = null;

                if (row.Attempts >= MaxAttempts)
                {
                    // MSG-004: out of the retry stream, into triage. ERROR level
                    // is the alert channel until the Phase-2 metrics pipeline.
                    row.DeadLetteredAtUtc = clock.UtcNow;
                    LogEventDeadLettered(logger, ex, row.Id, row.EventType, row.Attempts);
                }
                else
                {
                    row.NextAttemptAtUtc = clock.UtcNow + ComputeBackoff(row.Attempts);
                    LogEventFailed(logger, ex, row.Id, row.EventType, row.Attempts);
                }
            }
        }

        if (batch.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return batch.Count;
    }

    /// <summary>
    /// Claims up to <see cref="BatchSize"/> due rows for this processor. On
    /// PostgreSQL the claim is FOR UPDATE SKIP LOCKED plus a lease stamp, so
    /// concurrent claimants receive disjoint rows and a crashed claimant's rows
    /// become reclaimable when the lease lapses. On non-relational providers
    /// (unit tests) the same due-filter runs without the cross-process lock.
    /// </summary>
    internal static async Task<List<OutboxEvent>> ClaimDueBatchAsync(
        AppDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (db.Database.IsNpgsql())
        {
            var leaseUntil = now + ClaimLease;
            var claimed = await db.Set<OutboxEvent>().FromSqlInterpolated($"""
                WITH due AS (
                    SELECT id FROM qams.outbox_event
                    WHERE processed_at_utc IS NULL
                      AND dead_lettered_at_utc IS NULL
                      AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {now})
                      AND (claimed_until_utc IS NULL OR claimed_until_utc <= {now})
                    ORDER BY occurred_at_utc
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE qams.outbox_event AS o
                SET claimed_until_utc = {leaseUntil}
                FROM due
                WHERE o.id = due.id
                RETURNING o.*
                """).ToListAsync(cancellationToken);
            return [.. claimed.OrderBy(e => e.OccurredAtUtc)];
        }

        return await db.Set<OutboxEvent>()
            .Where(e => e.ProcessedAtUtc == null
                        && e.DeadLetteredAtUtc == null
                        && (e.NextAttemptAtUtc == null || e.NextAttemptAtUtc <= now)
                        && (e.ClaimedUntilUtc == null || e.ClaimedUntilUtc <= now))
            .OrderBy(e => e.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// MSG-005: exponential backoff (base × 2^(attempts−1)) with up to 25%
    /// additive jitter so a burst of failures does not retry in lock-step.
    /// </summary>
    internal static TimeSpan ComputeBackoff(int attempts)
    {
        var baseDelay = BackoffBase * Math.Pow(2, attempts - 1);
        var jitter = baseDelay * (Random.Shared.NextDouble() * 0.25);
        return baseDelay + jitter;
    }

    private async Task RunRetentionPurgeAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Elevate();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = clock.UtcNow - TimeSpan.FromDays(options.RetentionDays);
        var purged = await PurgeProcessedAsync(db, cutoff, cancellationToken);
        if (purged > 0)
        {
            LogPurged(logger, purged, options.RetentionDays);
        }
    }

    /// <summary>
    /// MSG-007: deletes processed rows older than the cutoff. Delivered events
    /// live on in the hash-chained audit ledger — the outbox row is transport,
    /// not the record — so the purge never touches unprocessed or dead-lettered
    /// rows.
    /// </summary>
    internal static Task<int> PurgeProcessedAsync(
        AppDbContext db, DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        db.Set<OutboxEvent>()
            .Where(e => e.ProcessedAtUtc != null && e.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

    private static INotification Deserialize(OutboxEvent row)
    {
        var eventType = Type.GetType(row.EventType)
            ?? throw new InvalidOperationException($"Unknown outbox event type '{row.EventType}'.");

        var domainEvent = JsonSerializer.Deserialize(row.Payload, eventType, SerializerOptions)
            ?? throw new InvalidOperationException($"Outbox payload {row.Id} deserialized to null.");

        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
        return (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox event {EventId} ({EventType}) failed, attempt {Attempts} — retrying with backoff")]
    private static partial void LogEventFailed(
        ILogger logger, Exception ex, Guid eventId, string eventType, int attempts);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox event {EventId} ({EventType}) DEAD-LETTERED after {Attempts} attempts — " +
                  "manual triage required (qams.outbox_event WHERE dead_lettered_at_utc IS NOT NULL)")]
    private static partial void LogEventDeadLettered(
        ILogger logger, Exception ex, Guid eventId, string eventType, int attempts);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Outbox retention purge removed {Count} processed row(s) older than {RetentionDays} day(s)")]
    private static partial void LogPurged(ILogger logger, int count, int retentionDays);
}
