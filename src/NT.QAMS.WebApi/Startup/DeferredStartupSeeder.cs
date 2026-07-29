namespace NT.QAMS.WebApi.Startup;

/// <summary>
/// OPS-010: carries the startup seeding to completion when the pre-listen
/// attempt had to defer because the database was unreachable. It runs after the
/// host is serving, so <c>/health/ready</c> reports the outage while this
/// retries; on a healthy boot the inline attempt already succeeded and this
/// service exits immediately.
/// </summary>
public sealed class DeferredStartupSeeder(
    StartupSeedingState state,
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<DeferredStartupSeeder> logger) : BackgroundService
{
    /// <summary>How long to wait before re-probing a database that was down.</summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (state.Completed)
        {
            return; // the inline attempt already seeded — nothing deferred
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RetryInterval, stoppingToken);

                if (await StartupSeeding.TryRunAsync(services, configuration, logger, stoppingToken))
                {
                    state.Completed = true;
                    logger.LogInformation(
                        "Deferred startup data seeding completed — the database became available.");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return; // normal shutdown
            }
            catch (Exception ex)
            {
                // Never take the host down from a background thread: log it and
                // stop retrying, because this is a real fault rather than an
                // unreachable database (TryRunAsync swallows only the latter).
                logger.LogError(ex,
                    "Deferred startup data seeding failed and will not be retried. The platform administrator " +
                    "bootstrap and/or the starter list-of-values backfill did not run; investigate and restart.");
                return;
            }
        }
    }
}
