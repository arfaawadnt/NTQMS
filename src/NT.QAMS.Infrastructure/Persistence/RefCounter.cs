using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Persistence;

/// <summary>Counter row per (tenant, ref type, year) — the numbering source of truth.</summary>
public sealed class RefCounter
{
    public Guid TenantId { get; init; }
    public string RefType { get; init; } = null!;
    public int Year { get; init; }
    public long LastValue { get; set; }
}

/// <summary>
/// Race-free reference issuance: one atomic upsert-returning statement inside the
/// caller's transaction. Gapless within committed history; the row lock serializes
/// per (tenant, type, year) — deliberate and harmless at business-record rates.
/// </summary>
public sealed class PostgresReferenceNumberGenerator(AppDbContext db, IClock clock)
    : IReferenceNumberGenerator
{
    public async Task<string> NextAsync(Guid tenantId, string refType, CancellationToken cancellationToken)
    {
        var year = clock.UtcNow.Year;

        // Enumerated without LINQ composition: operators such as SingleAsync make
        // EF wrap the SQL in an outer SELECT, and INSERT … RETURNING is not
        // composable as a subquery on PostgreSQL (runtime failure invisible to
        // the InMemory test provider). ToListAsync executes the statement verbatim.
        var next = (await db.Database
            .SqlQuery<long>($"""
                INSERT INTO qams.ref_counter (tenant_id, ref_type, year, last_value)
                VALUES ({tenantId}, {refType}, {year}, 1)
                ON CONFLICT (tenant_id, ref_type, year)
                DO UPDATE SET last_value = qams.ref_counter.last_value + 1
                RETURNING last_value AS "Value"
                """)
            .ToListAsync(cancellationToken))
            .Single();

        return $"{refType}-{year}-{next:0000}";
    }
}
