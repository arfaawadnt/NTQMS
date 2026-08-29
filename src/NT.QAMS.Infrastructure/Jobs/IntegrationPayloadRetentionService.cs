using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Jobs;

/// <summary>
/// ADT payload retention (M-12 / ADR-0011): once a message is past its retention
/// window (default 90 days, <c>Integration:PayloadRetentionDays</c>), its stored
/// raw payload is dropped while the row — status, error, timings — stays as the
/// durable interface-health record. Runs across all tenants under leader
/// election, like the compliance sweep; idempotent (a purged row matches no
/// query on the next round).
/// </summary>
public sealed partial class IntegrationPayloadRetentionService(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    IConfiguration configuration,
    ILogger<IntegrationPayloadRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private const string PurgedTombstone = "«purged»";

    private int RetentionDays =>
        int.TryParse(configuration["Integration:PayloadRetentionDays"], out var days)
            ? Math.Clamp(days, 1, 3650)
            : 90;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var purged = await RunAsync(stoppingToken);
                if (purged > 0)
                {
                    LogPurged(logger, purged, RetentionDays);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogFailed(logger, ex);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task<int> RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Elevate();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var purged = 0;
        await AdvisoryLock.TryRunExclusiveAsync(
            db, AdvisoryLockKeys.IntegrationPayloadRetention,
            async () => purged = await PurgeAsync(db, ct), ct);
        return purged;
    }

    private Task<int> PurgeAsync(AppDbContext db, CancellationToken ct) =>
        PurgeOlderThanAsync(db, clock.UtcNow.AddDays(-RetentionDays), ct);

    /// <summary>
    /// Tombstones the raw payload of every settled (Processed/Failed) message
    /// received before <paramref name="cutoff"/> that still holds a real payload.
    /// Bulk update — no aggregate invariant to protect — and idempotent: a
    /// tombstoned row matches no subsequent round.
    /// </summary>
    internal static Task<int> PurgeOlderThanAsync(AppDbContext db, DateTimeOffset cutoff, CancellationToken ct) =>
        db.IntegrationMessages
            .IgnoreQueryFilters()
            .Where(m => m.ReceivedAtUtc < cutoff
                        && m.Status != MessageStatus.Received
                        && m.RawPayload != PurgedTombstone)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.RawPayload, PurgedTombstone), ct);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Purged {Count} ADT message payload(s) older than {Days} days.")]
    private static partial void LogPurged(ILogger logger, int count, int days);

    [LoggerMessage(Level = LogLevel.Error, Message = "ADT payload retention purge failed.")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
