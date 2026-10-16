using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// EP05/EP15 gap closure against a REAL PostgreSQL server: the resolved design
/// and the series shape are fenced by CHECK constraints, and the two new
/// cross-aggregate references (MU component → precision study, sigma
/// assessment → precision study) are tenant-composite FKs — a consumer can
/// cite a study only in its own tenant, and never a study that does not exist.
/// </summary>
[Collection("real-postgres")]
public sealed class PrecisionStudyConstraintTests(RealPostgresFixture fx)
{
    private const string CheckViolation = "23514";
    private const string FkViolation = "23503";
    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static PrecisionStudy SignedOffStudy(Guid tenant)
    {
        var study = PrecisionStudy.Configure($"PR-IT-{Guid.NewGuid():N}"[..24], "Glucose", "mmol/L", "L1", null, null);
        ((ITenantScoped)study).TenantId = tenant;
        study.AddMeasurement("R1", 10m, "D1", PrecisionConditions.None);
        study.AddMeasurement("R1", 12m, "D1", PrecisionConditions.None);
        study.AddMeasurement("R1", 14m, "D2", PrecisionConditions.None);
        study.AddMeasurement("R1", 16m, "D2", PrecisionConditions.None);
        study.Calculate();
        study.SignOff(Guid.CreateVersion7(), At);
        return study;
    }

    [SkippableFact]
    public async Task Design_and_series_shape_are_fenced_by_the_database()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        // An editable (not signed-off) study, so the frozen-record trigger
        // stays out of the way and only the CHECKs are probed.
        var study = PrecisionStudy.Configure($"PR-IT-{Guid.NewGuid():N}"[..24], "Glucose", "mmol/L", "L1", null, null);
        ((ITenantScoped)study).TenantId = Guid.CreateVersion7();
        db.PrecisionStudies.Add(study);
        await db.SaveChangesAsync();

        foreach (var attack in new[]
                 {
                     "UPDATE qams.precision_study SET design = 'Bogus' WHERE id = {0}",
                     "UPDATE qams.precision_study SET series_level_count = 3 WHERE id = {0}",
                     "UPDATE qams.precision_study SET series_ref = 'PRS-2026-0001' WHERE id = {0}",
                     "UPDATE qams.precision_study SET series_ref = 'PRS-2026-0001', series_level_count = 11 WHERE id = {0}",
                 })
        {
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT probe");
            var act = () => db.Database.ExecuteSqlRawAsync(attack, study.Id);
            (await Assert.ThrowsAsync<PostgresException>(act)).SqlState.Should().Be(CheckViolation, attack);
            await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT probe");
        }

        // The legal shapes pass — including the multi-site designs.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE qams.precision_study SET design = 'DaysAndRuns', series_ref = 'PRS-2026-0001', series_level_count = 2 WHERE id = {0}",
            study.Id);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE qams.precision_study SET design = 'SitesDaysAndRuns' WHERE id = {0}", study.Id);

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Consumers_can_cite_a_precision_study_only_within_their_own_tenant()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        // Parents in their own save: EF does not know the SQL-declared FKs.
        var study = SignedOffStudy(tenantA);
        db.PrecisionStudies.Add(study);
        await db.SaveChangesAsync();

        // Same tenant, through the aggregates: accepted by the database.
        var budget = UncertaintyBudget.Create("MU-IT-1", "Glucose", "Hexokinase", "mmol/L", "L1", 2m, null);
        ((ITenantScoped)budget).TenantId = tenantA;
        budget.AddPrecisionComponent(study);
        var sigma = SigmaAssessment.Create("SGA-IT-1", "Glucose", "mmol/L", 50m, 1m, 1m);
        ((ITenantScoped)sigma).TenantId = tenantA;
        sigma.UsePrecisionStudy(study);
        db.UncertaintyBudgets.Add(budget);
        db.SigmaAssessments.Add(sigma);
        await db.SaveChangesAsync();

        // Tenant B points at tenant A's study, and anyone points at nothing: 23503.
        var foreign = SigmaAssessment.Create("SGA-IT-2", "Glucose", "mmol/L", 50m, 1m, 1m);
        ((ITenantScoped)foreign).TenantId = tenantB;
        db.SigmaAssessments.Add(foreign);
        await db.SaveChangesAsync();

        foreach (var (sql, target) in new (string, Guid)[]
                 {
                     ("UPDATE qams.sigma_assessment SET source_precision_study_id = {0} WHERE id = {1}", foreign.Id),
                     ("UPDATE qams.sigma_assessment SET source_precision_study_id = {0} WHERE id = {1}", sigma.Id),
                     ("UPDATE qams.uncertainty_component SET source_precision_study_id = {0} WHERE budget_id = {1}", budget.Id),
                 })
        {
            var studyId = target == foreign.Id ? study.Id : Guid.CreateVersion7();
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT probe");
            var act = () => db.Database.ExecuteSqlRawAsync(sql, studyId, target);
            (await Assert.ThrowsAsync<PostgresException>(act)).SqlState.Should().Be(FkViolation, sql);
            await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT probe");
        }

        await tx.RollbackAsync();
    }
}
