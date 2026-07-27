using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Phase-1 findings MSG-004/005/007 + OPS-002 (durable) against a REAL
/// PostgreSQL server: outbox rows are claimed with FOR UPDATE SKIP LOCKED
/// under a lease so two processors publish each row once; a claim lease
/// expires and the row is reclaimable; the retention purge deletes only
/// processed rows past the window. Rows must be committed for concurrent
/// sessions to see them, so these tests clean up after themselves.
/// </summary>
[Collection("real-postgres")]
public sealed class OutboxResilienceTests(RealPostgresFixture fx)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    private static OutboxEvent NewRow(string marker, DateTimeOffset occurredAt) => new()
    {
        Id = Guid.CreateVersion7(),
        EventType = marker,
        Payload = "{}",
        OccurredAtUtc = occurredAt,
    };

    private async Task CleanupAsync(string marker)
    {
        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await db.Set<OutboxEvent>().Where(e => e.EventType == marker).ExecuteDeleteAsync();
    }

    [SkippableFact]
    public async Task Two_concurrent_claimants_receive_disjoint_rows()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");
        var marker = $"ITEST-CLAIM-{Guid.NewGuid():N}";

        using var seed = fx.CreateContext(out var seedCtx);
        seedCtx.Elevate();
        for (var i = 0; i < 20; i++)
        {
            seed.Set<OutboxEvent>().Add(NewRow(marker, Now.AddSeconds(i)));
        }

        await seed.SaveChangesAsync();

        try
        {
            using var first = fx.CreateContext(out var firstCtx);
            firstCtx.Elevate();
            using var second = fx.CreateContext(out var secondCtx);
            secondCtx.Elevate();

            // Race two claimants exactly as two replicas' processors would.
            var claims = await Task.WhenAll(
                OutboxProcessor.ClaimDueBatchAsync(first, Now, CancellationToken.None),
                OutboxProcessor.ClaimDueBatchAsync(second, Now, CancellationToken.None));

            var mine = claims.SelectMany(batch => batch)
                .Where(e => e.EventType == marker)
                .Select(e => e.Id)
                .ToList();

            mine.Should().OnlyHaveUniqueItems("SKIP LOCKED + lease must hand each row to exactly one claimant");
            mine.Should().HaveCount(20, "between them the two claimants drain the whole backlog");
        }
        finally
        {
            await CleanupAsync(marker);
        }
    }

    [SkippableFact]
    public async Task A_claim_leases_the_row_until_the_lease_expires()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");
        var marker = $"ITEST-LEASE-{Guid.NewGuid():N}";

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        db.Set<OutboxEvent>().Add(NewRow(marker, Now));
        await db.SaveChangesAsync();

        try
        {
            using var claimant = fx.CreateContext(out var claimCtx);
            claimCtx.Elevate();

            (await OutboxProcessor.ClaimDueBatchAsync(claimant, Now, CancellationToken.None))
                .Should().Contain(e => e.EventType == marker, "the fresh row is due and unclaimed");

            using var rival = fx.CreateContext(out var rivalCtx);
            rivalCtx.Elevate();

            (await OutboxProcessor.ClaimDueBatchAsync(rival, Now.AddSeconds(1), CancellationToken.None))
                .Should().NotContain(e => e.EventType == marker, "the row is leased to the first claimant");

            (await OutboxProcessor.ClaimDueBatchAsync(
                    rival, Now + OutboxProcessor.ClaimLease + TimeSpan.FromSeconds(1), CancellationToken.None))
                .Should().Contain(e => e.EventType == marker,
                    "a crashed claimant's rows become reclaimable when the lease lapses");
        }
        finally
        {
            await CleanupAsync(marker);
        }
    }

    [SkippableFact]
    public async Task Retention_purge_deletes_only_processed_rows_past_the_window()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");
        var marker = $"ITEST-PURGE-{Guid.NewGuid():N}";
        var cutoff = Now.AddDays(-30);

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        var processedOld = NewRow(marker, Now.AddDays(-40));
        processedOld.ProcessedAtUtc = Now.AddDays(-40);
        var processedRecent = NewRow(marker, Now.AddDays(-10));
        processedRecent.ProcessedAtUtc = Now.AddDays(-10);
        var unprocessedOld = NewRow(marker, Now.AddDays(-40));
        db.Set<OutboxEvent>().AddRange(processedOld, processedRecent, unprocessedOld);
        await db.SaveChangesAsync();

        try
        {
            var purged = await OutboxProcessor.PurgeProcessedAsync(db, cutoff, CancellationToken.None);

            purged.Should().BeGreaterThanOrEqualTo(1);
            var survivors = await db.Set<OutboxEvent>().AsNoTracking()
                .Where(e => e.EventType == marker).Select(e => e.Id).ToListAsync();
            survivors.Should().NotContain(processedOld.Id, "processed and past the retention window");
            survivors.Should().Contain(processedRecent.Id, "processed but still inside the window");
            survivors.Should().Contain(unprocessedOld.Id, "never delivered — the purge must not lose it");
        }
        finally
        {
            await CleanupAsync(marker);
        }
    }
}
