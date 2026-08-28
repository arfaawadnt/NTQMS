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
}
