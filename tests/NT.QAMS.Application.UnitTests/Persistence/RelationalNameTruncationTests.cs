using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NT.QAMS.Infrastructure.Persistence;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Persistence;

/// <summary>
/// Audit finding M-14: PostgreSQL truncates identifiers at 63 bytes and EF
/// truncates client-side at 62 — silently, mid-word ("…tenant_id_trai"). Any
/// foreign key whose CONVENTION name would exceed 62 characters must be pinned
/// with <c>HasConstraintName</c> using the CLAUDE.md §5 abbreviation map, and
/// no pinned name may itself exceed the limit. Model-only — no database.
/// </summary>
public class RelationalNameTruncationTests
{
    private const int MaxIdentifier = 62;

    /// <summary>
    /// Pre-baseline truncations, frozen as-is (renaming them is schema churn
    /// outside finding M-14's scope). SHRINK-ONLY: fix one → delete its row.
    /// Never add a row — pin new names with HasConstraintName instead.
    /// </summary>
    private static readonly HashSet<string> LegacyTruncations = new(StringComparer.Ordinal)
    {
        "fk_detection_measurement_detection_limit_study_tenant_id_study",
        "fk_instrument_reading_instrument_comparability_study_tenant_id",
        "fk_interference_measurement_interference_study_tenant_id_study",
    };

    [Fact]
    public void Every_foreign_key_whose_convention_name_would_truncate_is_explicitly_pinned()
    {
        // Mirrors the production options (DependencyInjection.cs): the snake_case
        // plugin rewrites constraint names at model finalisation, so it must be
        // present for the store names to be the real ones.
        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=model-only;Database=model-only;Username=x")
                .UseSnakeCaseNamingConvention()
                .Options,
            new FakeCurrentTenant());

        var offenders = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            var schema = entity.GetSchema();
            if (tableName is null)
            {
                continue;
            }

            var table = StoreObjectIdentifier.Table(tableName, schema);
            foreach (var fk in entity.GetForeignKeys())
            {
                if (fk.PrincipalEntityType.GetTableName() is not { } principal)
                {
                    continue;
                }

                var principalTable = StoreObjectIdentifier.Table(principal, fk.PrincipalEntityType.GetSchema());
                var actual = fk.GetConstraintName(table, principalTable);
                if (actual is null)
                {
                    continue;
                }

                if (actual.Length > MaxIdentifier)
                {
                    offenders.Add($"{tableName}: pinned name '{actual}' is over {MaxIdentifier} chars");
                    continue;
                }

                // A silent truncation reads as a strict PREFIX of the intended
                // convention name (the cut is mid-word). A deliberate
                // HasConstraintName pin uses abbreviations and is no prefix.
                var columns = fk.Properties.Select(p => p.GetColumnName(table) ?? p.Name);
                var convention = $"fk_{tableName}_{principal}_{string.Join("_", columns)}";
                if (actual != convention
                    && convention.StartsWith(actual, StringComparison.Ordinal)
                    && !LegacyTruncations.Contains(actual))
                {
                    offenders.Add(
                        $"{tableName}: convention name '{convention}' ({convention.Length} chars) "
                        + $"silently truncates to '{actual}' — pin it with HasConstraintName");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Every over-long FK name must be a deliberate, readable, pinned identifier (CLAUDE.md §5):\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// Pre-baseline unique indexes that shipped with EF's default <c>ix_</c>
    /// prefix, frozen as-is (renaming them is schema churn outside finding
    /// N-04's scope). SHRINK-ONLY: rename one → delete its row here.
    /// </summary>
    private static readonly HashSet<string> LegacyUniqueIndexNames = new(StringComparer.Ordinal)
    {
        "ix_archive_entry_tenant_id_source_module_source_ref",
        "ix_audit_tenant_id_audit_ref",
        "ix_audit_trail_review_tenant_id_review_ref",
        "ix_branch_tenant_id_code",
        "ix_carryover_study_tenant_id_study_ref",
        "ix_change_request_tenant_id_change_ref",
        "ix_complaint_tenant_id_complaint_ref",
        "ix_conflict_declaration_tenant_id_conflict_ref",
        "ix_context_issue_tenant_id_issue_ref",
        "ix_controlled_document_tenant_id_code",
        "ix_department_tenant_id_branch_id_code",
        "ix_detection_limit_study_tenant_id_study_ref",
        "ix_equipment_item_tenant_id_code",
        "ix_equipment_item_tenant_id_serial_number",
        "ix_feedback_entry_tenant_id_feedback_ref",
        "ix_instrument_comparability_study_tenant_id_study_ref",
        "ix_audit_trail_tenant_id_sequence",
        "ix_kpi_snapshot_tenant_id_date",
        "ix_tenant_identifier",
        "ix_interested_party_tenant_id_party_ref",
        "ix_interference_study_tenant_id_study_ref",
        "ix_linearity_study_tenant_id_study_ref",
        "ix_lot_comparison_study_tenant_id_study_ref",
        "ix_lov_entry_tenant_id_category_code",
        "ix_management_review_tenant_id_review_ref",
        "ix_method_comparison_study_tenant_id_study_ref",
        "ix_monitoring_point_tenant_id_point_ref",
        "ix_nonconformance_tenant_id_nc_ref",
        "ix_outlier_screening_tenant_id_screening_ref",
        "ix_precision_study_tenant_id_study_ref",
        "ix_pt_enrollment_tenant_id_pt_ref",
        "ix_pt_plan_tenant_id_plan_ref",
        "ix_pt_plan_tenant_id_year",
        "ix_quality_objective_tenant_id_objective_ref",
        "ix_quality_policy_tenant_id_policy_ref",
        "ix_quality_policy_tenant_id_version",
        "ix_reference_interval_study_tenant_id_study_ref",
        "ix_reference_standard_tenant_id_standard_ref",
        "ix_risk_item_tenant_id_risk_ref",
        "ix_role_tenant_id_normalized_name",
        "ix_sigma_assessment_tenant_id_assessment_ref",
        "ix_sla_definition_tenant_id_module_severity",
        "ix_supplier_tenant_id_supplier_ref",
        "ix_test_catalog_item_tenant_id_test_code",
        "ix_uncertainty_budget_tenant_id_budget_ref",
        "ix_user_access_review_tenant_id_review_ref",
        "ix_user_account_tenant_id_email",
        "ix_validation_study_tenant_id_study_ref",
    };

    [Fact]
    public void Every_unique_index_uses_the_ux_prefix()
    {
        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=model-only;Database=model-only;Username=x")
                .UseSnakeCaseNamingConvention()
                .Options,
            new FakeCurrentTenant());

        var offenders = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is null)
            {
                continue;
            }

            var table = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
            foreach (var index in entity.GetIndexes().Where(i => i.IsUnique))
            {
                var name = index.GetDatabaseName(table);
                if (name is null
                    || name.StartsWith("ux_", StringComparison.Ordinal)
                    || LegacyUniqueIndexNames.Contains(name))
                {
                    continue;
                }

                offenders.Add($"{tableName}: unique index '{name}' must use the ux_ prefix (CLAUDE.md §5)");
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void No_free_text_column_is_a_bounded_varchar_of_1000_or_more()
    {
        // Schema hardening 1.2 / audit finding N-05: free text sized >= 1000 is
        // `text` — the bound lives in the command validator, not the column.
        // Bounded codes, refs, enum strings and hashes keep varchar(n) < 1000.
        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=model-only;Database=model-only;Username=x")
                .UseSnakeCaseNamingConvention()
                .Options,
            new FakeCurrentTenant());

        var offenders = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is null)
            {
                continue;
            }

            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(string) && property.GetMaxLength() is >= 1000 and var length)
                {
                    offenders.Add($"{tableName}.{property.GetColumnName()}: varchar({length}) — make it text "
                        + "and keep the bound in the validator (CLAUDE.md §5, hardening 1.2)");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }
}
