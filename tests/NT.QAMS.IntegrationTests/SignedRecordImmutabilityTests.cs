using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Verifies audit finding F-02 against a REAL PostgreSQL server: once a record
/// is signed off, the database itself (not just the domain) rejects any UPDATE
/// or DELETE — while still allowing the legitimate transition INTO the signed
/// state. Everything runs inside a rolled-back transaction, so the otherwise
/// un-deletable signed row never persists.
/// </summary>
[Collection("real-postgres")]
public sealed class SignedRecordImmutabilityTests(RealPostgresFixture fx)
{
    private static readonly DateTimeOffset At = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Sign_off_transition_succeeds_then_raw_update_is_rejected()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        var tenant = Guid.CreateVersion7();
        ctx.Set(tenant);
        await using var tx = await db.Database.BeginTransactionAsync();

        var signed = await SeedSignedScreeningAsync(db, tenant);

        // The trigger must NOT have blocked the transition into SignedOff.
        signed.State.Should().Be(OutlierScreeningState.SignedOff);

        var tamper = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE qams.outlier_screening SET dataset = 'TAMPERED' WHERE id = {0}", signed.Id);

        (await tamper.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("23514", "the immutability trigger raises a check_violation");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Signed_record_raw_delete_is_rejected()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        var tenant = Guid.CreateVersion7();
        ctx.Set(tenant);
        await using var tx = await db.Database.BeginTransactionAsync();

        var signed = await SeedSignedScreeningAsync(db, tenant);

        var delete = async () => await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM qams.outlier_screening WHERE id = {0}", signed.Id);

        (await delete.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("23514");

        await tx.RollbackAsync();
    }

    /// <summary>Create → enter points → calculate → sign off, all persisted in the current transaction.</summary>
    private static async Task<OutlierScreening> SeedSignedScreeningAsync(AppDbContext db, Guid tenant)
    {
        var s = OutlierScreening.Configure("ITEST-SIGN", "dataset", "u");
        ((ITenantScoped)s).TenantId = tenant;
        foreach (var v in new[] { 10m, 11m, 12m, 13m, 100m })
        {
            s.AddPoint(v, "p");
        }

        db.OutlierScreenings.Add(s);
        await db.SaveChangesAsync();

        s.Calculate();
        await db.SaveChangesAsync();

        s.SignOff(Guid.CreateVersion7(), At); // transition INTO SignedOff — must be allowed
        await db.SaveChangesAsync();
        return s;
    }
}
