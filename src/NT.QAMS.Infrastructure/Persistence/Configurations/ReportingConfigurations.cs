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

/// <summary>
/// The tenant's Quality Health Score weighting. One profile per tenant, enforced
/// by a unique index on the tenant alone — the score has a single definition, and
/// two competing weightings would make the reported figure ambiguous.
/// </summary>
public sealed class QualityHealthProfileConfiguration : IEntityTypeConfiguration<QualityHealthProfile>
{
    public void Configure(EntityTypeBuilder<QualityHealthProfile> builder)
    {
        builder.ToTable("quality_health_profile", "qams");
        builder.HasKey(p => new { p.TenantId, p.Id });
        builder.HasIndex(p => p.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_quality_health_profile_tenant");

        builder.OwnsMany(p => p.Weights, weight =>
        {
            weight.ToTable("quality_health_weight", "qams");
            weight.Property<Guid>("TenantId");
            // Pinned: the EF-default name truncates mid-word at the 62-char limit.
            weight.WithOwner().HasForeignKey("TenantId", "profile_id")
                .HasConstraintName("fk_quality_health_weight_profile");
            weight.HasKey("TenantId", "Id");
            weight.Property(w => w.Category).HasConversion<string>().HasMaxLength(30);
            weight.Property(w => w.Weight);
            weight.HasIndex("TenantId", "profile_id", "Category")
                .IsUnique()
                .HasDatabaseName("ux_quality_health_weight_category");
        });

        builder.Ignore(p => p.DomainEvents);
    }
}
