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

    /// <summary>
    /// Schema hardening Phase 3: the value-domain net now covers every
    /// enum-backed column (67 domains) plus the five hash columns. One
    /// representative per new constraint family, probed by direct SQL.
    /// </summary>
    [SkippableFact]
    public async Task Phase3_domains_reject_bogus_enum_values_and_malformed_hashes()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Enum domain on a Phase-3 table (equipment status - EquipmentStatus enum).
        await db.Database.ExecuteSqlRawAsync("SAVEPOINT p1");
        var statusAttack = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO qams.equipment_item (id, tenant_id, code, name, serial_number, status, calibration_interval_days, grace_period_days, created_at_utc) " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), 'CK-P3', 'probe', 'SN-1', 'Exploded', 30, 0, now())");
        (await Assert.ThrowsAsync<PostgresException>(statusAttack)).SqlState.Should().Be(CheckViolation);
        await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT p1");

        // Literal-set domain on the append-only ledger (field_change.action).
        var actionAttack = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO audit.field_change (id, entity_type, entity_id, action, property, occurred_at_utc, actor) " +
            "VALUES (gen_random_uuid(), 'Probe', gen_random_uuid(), 'Nuked', 'p', now(), 'itest')");
        (await Assert.ThrowsAsync<PostgresException>(actionAttack)).SqlState.Should().Be(CheckViolation);
        await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT p1");

        // Hash format: 64 chars of the wrong case is not a valid entry hash.
        var hashAttack = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO qams.file_reference (id, tenant_id, file_name, content_type, size_bytes, sha256, storage_key, created_at_utc) " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), 'p.pdf', 'application/pdf', 1, upper(repeat('ab', 32)), 'x', now())");
        (await Assert.ThrowsAsync<PostgresException>(hashAttack)).SqlState.Should().Be(CheckViolation);

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Postgres_rejects_out_of_range_hqms_numerics()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();
        var tenant = Guid.CreateVersion7();

        // Valid records, created the only sanctioned way — through the aggregates.
        var fmea = NT.QAMS.Domain.RiskGovernance.FmeaStudy.Create(
            "FME-ITEST-1", "constraint probe", "medication administration",
            NT.QAMS.Domain.RiskGovernance.FmeaType.Fmea);
        ((ITenantScoped)fmea).TenantId = tenant;
        var modeId = fmea.AddFailureMode("dispense", "wrong dose", "harm", "lookalike vials", 5, 5, 5);
        db.FmeaStudies.Add(fmea);

        var program = NT.QAMS.Domain.AuditManagement.AuditProgram.Create(2026, "constraint probe");
        ((ITenantScoped)program).TenantId = tenant;
        var plannedId = program.AddPlannedAudit(
            "pharmacy", null, null, NT.QAMS.Domain.AuditManagement.PlannedAuditPriority.Low, 2);
        db.AuditPrograms.Add(program);

        var indicator = NT.QAMS.Domain.QualityIndicators.QualityIndicator.Define(
            "IND-ITEST-1", "ITEST-RATE", "constraint probe", null, "events", "patient days", "per 1000",
            1000m, NT.QAMS.Domain.QualityIndicators.IndicatorFrequency.Monthly,
            NT.QAMS.Domain.QualityIndicators.IndicatorDirection.LowerIsBetter);
        ((ITenantScoped)indicator).TenantId = tenant;
        db.QualityIndicators.Add(indicator);

        var set = NT.QAMS.Domain.Accreditation.StandardSet.Define(
            NT.QAMS.Domain.Accreditation.AccreditationFramework.Other, "constraint probe", "v1");
        ((ITenantScoped)set).TenantId = tenant;
        var elementId = set.AddElement("CH1", "Chapter one", "STD1", "EL1", "element text", 1);
        db.StandardSets.Add(set);

        await db.SaveChangesAsync();

        // N-05: direct-SQL corruption of the HQMS numeric domains dies at the
        // database — FMEA scales, program quarters/years, rate factors, weights.
        var probes = new (string Name, string Sql, Guid Id)[]
        {
            ("FMEA severity 11", "UPDATE qams.fmea_failure_mode SET severity = 11 WHERE id = {0}", modeId),
            ("FMEA occurrence 0", "UPDATE qams.fmea_failure_mode SET occurrence = 0 WHERE id = {0}", modeId),
            ("FMEA detection 11", "UPDATE qams.fmea_failure_mode SET detection = 11 WHERE id = {0}", modeId),
            ("FMEA rpn 1001", "UPDATE qams.fmea_failure_mode SET rpn = 1001 WHERE id = {0}", modeId),
            ("planned quarter 5", "UPDATE qams.planned_audit SET planned_quarter = 5 WHERE id = {0}", plannedId),
            ("program year 1899", "UPDATE qams.audit_program SET year = 1899 WHERE id = {0}", program.Id),
            ("rate factor 0", "UPDATE qams.quality_indicator SET rate_factor = 0 WHERE id = {0}", indicator.Id),
            ("element weight 0", "UPDATE qams.standard_element SET weight = 0 WHERE id = {0}", elementId),
        };

        foreach (var (name, sql, id) in probes)
        {
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT probe");
            var attack = () => db.Database.ExecuteSqlRawAsync(sql, id);
            (await Assert.ThrowsAsync<PostgresException>(attack))
                .SqlState.Should().Be(CheckViolation, $"'{name}' must be constrained in the database itself");
            await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT probe");
        }

        await tx.RollbackAsync();
    }
}
