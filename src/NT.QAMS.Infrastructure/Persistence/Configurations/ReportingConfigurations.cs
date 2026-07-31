using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Reporting;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>Daily KPI snapshot rows — the `read` schema per the database architecture.</summary>
public sealed class KpiSnapshotConfiguration : IEntityTypeConfiguration<KpiSnapshot>
{
    public void Configure(EntityTypeBuilder<KpiSnapshot> builder)
    {
        builder.ToTable("kpi_snapshot", "read");
        builder.HasKey(s => new { s.TenantId, s.Id });
        builder.HasIndex(s => new { s.TenantId, s.Date }).IsUnique();
    }
}
