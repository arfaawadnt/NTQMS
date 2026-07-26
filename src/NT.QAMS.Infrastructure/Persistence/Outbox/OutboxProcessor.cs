using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Persistence.Outbox;

/// <summary>
/// Polls unprocessed outbox rows and publishes them in-process. At-least-once:
/// a crash between publish and mark re-delivers; consumers are idempotent by
/// EventId. Failed events retry up to MaxAttempts with the error recorded.
/// </summary>
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
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

        var batch = await db.Set<OutboxEvent>()
            .Where(e => e.ProcessedAtUtc == null && e.Attempts < MaxAttempts)
            .OrderBy(e => e.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

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
            }
            catch (Exception ex)
            {
                row.Attempts++;
                row.LastError = ex.Message;
                LogEventFailed(logger, ex, row.Id, row.EventType, row.Attempts);
            }
        }

        if (batch.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return batch.Count;
    }

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
        Message = "Outbox event {EventId} ({EventType}) failed, attempt {Attempts}")]
    private static partial void LogEventFailed(
        ILogger logger, Exception ex, Guid eventId, string eventType, int attempts);
}
