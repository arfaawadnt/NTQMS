using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Phase-1 finding DB-009/VAL-003 against a REAL PostgreSQL server: every
/// aggregate root maps PostgreSQL's xmin as its optimistic-concurrency token,
/// so of two racing edits to one row exactly one wins and the loser surfaces
/// DbUpdateConcurrencyException (mapped to HTTP 409 by DomainExceptionHandler).
/// The row must be committed and visible to both sessions, so this test cleans
/// up after itself instead of rolling back.
/// </summary>
[Collection("real-postgres")]
public sealed class OptimisticConcurrencyTests(RealPostgresFixture fx)
{
    [SkippableFact]
    public async Task Two_racing_edits_exactly_one_wins_and_the_loser_conflicts()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        var tenantId = Guid.CreateVersion7();
        var screeningRef = $"ITEST-CC-{Guid.NewGuid():N}"[..28];

        using var seed = fx.CreateContext(out var seedCtx);
        seedCtx.Elevate();
        var screening = OutlierScreening.Configure(screeningRef, "dataset", "u");
        ((ITenantScoped)screening).TenantId = tenantId;
        seed.OutlierScreenings.Add(screening);
        await seed.SaveChangesAsync();

        try
        {
            using var first = fx.CreateContext(out var firstCtx);
            firstCtx.Elevate();
            using var second = fx.CreateContext(out var secondCtx);
            secondCtx.Elevate();

            var rowInFirst = await first.OutlierScreenings.IgnoreQueryFilters()
                .SingleAsync(s => s.Id == screening.Id);
            var rowInSecond = await second.OutlierScreenings.IgnoreQueryFilters()
                .SingleAsync(s => s.Id == screening.Id);

            // Both sessions edit the same loaded snapshot; the first commit wins.
            first.Entry(rowInFirst).Property(nameof(OutlierScreening.Dataset)).CurrentValue = "edited first";
            await first.SaveChangesAsync();

            second.Entry(rowInSecond).Property(nameof(OutlierScreening.Dataset)).CurrentValue = "edited second";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

            // The winner's edit is intact — the conflict lost nothing.
            using var verify = fx.CreateContext(out var verifyCtx);
            verifyCtx.Elevate();
            (await verify.OutlierScreenings.IgnoreQueryFilters()
                    .SingleAsync(s => s.Id == screening.Id))
                .Dataset.Should().Be("edited first");
        }
        finally
        {
            await seed.OutlierScreenings.IgnoreQueryFilters()
                .Where(s => s.Id == screening.Id)
                .ExecuteDeleteAsync();
        }
    }
}
