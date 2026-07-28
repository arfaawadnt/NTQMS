using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Phase-5 finding DB-005 against a REAL PostgreSQL server: the CHECK
/// constraints are the LAST line of defense — a valid record created through
/// the domain cannot afterwards be corrupted by direct SQL (out-of-scale
/// score, out-of-domain status), because PostgreSQL itself refuses.
/// </summary>
[Collection("real-postgres")]
public sealed class CheckConstraintTests(RealPostgresFixture fx)
{
    private const string CheckViolation = "23514";

    [SkippableFact]
    public async Task Postgres_rejects_an_out_of_scale_severity_and_a_bogus_status()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        // A perfectly valid record, created the only sanctioned way — through
        // the aggregate.
        var nc = Nonconformance.Raise(
            $"ITEST-CK-{Guid.NewGuid():N}"[..24], "constraint probe", "details", 3, 3,
            NcSourceType.Internal, Guid.CreateVersion7(), null);
        ((ITenantScoped)nc).TenantId = Guid.CreateVersion7();
        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync();

        // Direct-SQL corruption attempts must die at the database. Each probe
        // runs under a savepoint: a violation aborts the (sub)transaction, and
        // rolling back to the savepoint lets the next probe run.
        await db.Database.ExecuteSqlRawAsync("SAVEPOINT probe");
        var severityAttack = () => db.Database.ExecuteSqlAsync(
            $"UPDATE qams.nonconformance SET severity = 9 WHERE id = {nc.Id}");
        (await Assert.ThrowsAsync<PostgresException>(severityAttack))
            .SqlState.Should().Be(CheckViolation, "severity is constrained to 1–5 in the database itself");
        await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT probe");

        var statusAttack = () => db.Database.ExecuteSqlAsync(
            $"UPDATE qams.nonconformance SET status = 'Bogus' WHERE id = {nc.Id}");
        (await Assert.ThrowsAsync<PostgresException>(statusAttack))
            .SqlState.Should().Be(CheckViolation, "status is constrained to the NcStatus domain");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Postgres_rejects_a_completion_that_precedes_creation()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        var attack = () => db.Database.ExecuteSqlAsync(
            $"""
             UPDATE qams.work_task
             SET completed_at_utc = created_at_utc - interval '1 day'
             WHERE id = (SELECT id FROM qams.work_task LIMIT 1)
             """);

        // With no rows the UPDATE is a no-op — only assert when data exists.
        var hasRows = await db.WorkTasks.IgnoreQueryFilters().AnyAsync();
        Skip.IfNot(hasRows, "no work_task rows in this database to probe against");

        (await Assert.ThrowsAsync<PostgresException>(attack))
            .SqlState.Should().Be(CheckViolation, "completion can never precede creation");

        await tx.RollbackAsync();
    }
}
