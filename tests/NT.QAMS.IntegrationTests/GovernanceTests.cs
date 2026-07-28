using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NT.QAMS.Application.ComplianceLedger;
using NT.QAMS.Infrastructure.Compliance;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Phase-6 findings TEST-002/003 against a REAL PostgreSQL server:
/// migrations are reversible (the last one rounds down and back up without
/// error), and a MID-CHAIN tamper of the hash-chained audit trail is detected
/// with the exact broken sequence — the F-01/F-02 evidence story, proven.
/// </summary>
[Collection("real-postgres")]
public sealed class GovernanceTests(RealPostgresFixture fx)
{
    [SkippableFact]
    public async Task The_last_migration_reverts_and_reapplies_cleanly()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Skip.If(applied.Count < 2, "need at least two applied migrations to round-trip");

        var migrator = db.GetService<IMigrator>();
        var last = applied[^1];
        var previous = applied[^2];

        await migrator.MigrateAsync(previous);
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotContain(last,
            "Down() must actually revert");

        await migrator.MigrateAsync();
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(last,
            "Up() must reapply after a revert — migrations are additive AND reversible");
    }

    [SkippableFact]
    public async Task A_mid_chain_tamper_is_detected_at_the_exact_sequence()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        // A fresh tenant's chain: three genuine entries.
        var tenantId = Guid.CreateVersion7();
        var appender = new AuditTrailAppender(db);
        // Whole-second timestamps: PostgreSQL stores microseconds, so a raw
        // UtcNow (100ns ticks) would already differ once read back — the
        // production appender only ever hashes DB-read values.
        var occurredAt = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 3; i++)
        {
            await appender.AppendAsync(
                tenantId, Guid.CreateVersion7(), $"Test.Event{i}", $"{{\"n\":{i}}}",
                occurredAt.AddSeconds(i), CancellationToken.None);
        }

        await db.SaveChangesAsync();

        var store = (NT.QAMS.Application.Abstractions.IComplianceLedgerStore)new ComplianceLedgerStore(db, ctx);
        var intact = await new VerifyChainHandler(store).Handle(new VerifyChainQuery(tenantId), CancellationToken.None);
        intact.Ok.Should().BeTrue("the untouched chain verifies");
        intact.VerifiedEntries.Should().Be(3);

        // The insider scenario: someone with DDL rights disables the
        // append-only guard and edits the MIDDLE of the trail.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit.audit_trail DISABLE TRIGGER USER");
        await db.Database.ExecuteSqlAsync(
            $"UPDATE audit.audit_trail SET payload = '{{\"n\":666}}' WHERE tenant_id = {tenantId} AND sequence = 2");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit.audit_trail ENABLE TRIGGER USER");

        var tampered = await new VerifyChainHandler(store).Handle(new VerifyChainQuery(tenantId), CancellationToken.None);
        tampered.Ok.Should().BeFalse("the hash chain must expose the edit");
        tampered.BrokenAtSequence.Should().Be(2, "detection names the exact tampered entry");

        await tx.RollbackAsync();
    }
}
