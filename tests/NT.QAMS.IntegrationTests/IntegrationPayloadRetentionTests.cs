using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Infrastructure.Jobs;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// M-12 / ADR-0011 against a REAL PostgreSQL server: the ADT payload-retention
/// purge tombstones settled messages past the window (leaving the row as the
/// interface-health record) and leaves recent and still-Received messages
/// untouched. The purge uses <c>ExecuteUpdate</c>, which the in-memory provider
/// cannot run — so this is real-PG only.
/// </summary>
[Collection("real-postgres")]
public sealed class IntegrationPayloadRetentionTests(RealPostgresFixture fx)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Settled_messages_past_the_window_have_their_payload_purged()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        var tenant = Guid.CreateVersion7();
        ctx.Set(tenant);
        await using var tx = await db.Database.BeginTransactionAsync();

        var endpoint = IntegrationEndpoint.Register("ADT", InterfaceSystem.His, InterfaceProtocol.Hl7V2);
        ((ITenantScoped)endpoint).TenantId = tenant;
        db.IntegrationEndpoints.Add(endpoint);

        IntegrationMessage Msg(string key, DateTimeOffset at, bool process)
        {
            var m = IntegrationMessage.Receive(endpoint.Id, key, "ADT^A01", "MSH|old|payload", at);
            ((ITenantScoped)m).TenantId = tenant;
            if (process) { m.MarkProcessed(at); }
            return m;
        }

        var oldProcessed = Msg("OLD-1", Now.AddDays(-120), process: true);   // past window, settled → purge
        var recentProcessed = Msg("NEW-1", Now.AddDays(-10), process: true); // inside window → keep
        var oldReceived = Msg("OLD-2", Now.AddDays(-120), process: false);   // never settled → keep
        db.IntegrationMessages.AddRange(oldProcessed, recentProcessed, oldReceived);
        await db.SaveChangesAsync();

        var purged = await IntegrationPayloadRetentionService.PurgeOlderThanAsync(
            db, Now.AddDays(-90), CancellationToken.None);

        purged.Should().Be(1, "only the settled message past the 90-day window is purged");

        async Task<string> PayloadOf(Guid id) =>
            (await db.IntegrationMessages.AsNoTracking().IgnoreQueryFilters().SingleAsync(m => m.Id == id)).RawPayload;

        (await PayloadOf(oldProcessed.Id)).Should().Be("«purged»");
        (await PayloadOf(recentProcessed.Id)).Should().Contain("payload", "a recent message keeps its payload");
        (await PayloadOf(oldReceived.Id)).Should().Contain("payload", "an unsettled message keeps its payload");

        await tx.RollbackAsync();
    }
}
