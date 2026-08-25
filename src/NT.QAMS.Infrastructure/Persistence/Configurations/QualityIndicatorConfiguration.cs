using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.QualityIndicators;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the QualityIndicator aggregate (HQMS M06). Tenant-first composite
/// key, enum-as-string columns fenced by CHECK domains in the migration, decimal
/// precision on all measured quantities, and an owned measurement child carrying a
/// shadow tenant column and composite ownership FK. FORCE RLS on both tables is added
/// in the migration (EF does not generate it).
/// </summary>
public sealed class QualityIndicatorConfiguration : IEntityTypeConfiguration<QualityIndicator>
{
    public void Configure(EntityTypeBuilder<QualityIndicator> builder)
    {
        builder.ToTable("quality_indicator", "qams");
        builder.HasKey(i => new { i.TenantId, i.Id });

        builder.Property(i => i.IndicatorRef).HasMaxLength(30);
        builder.Property(i => i.Code).HasMaxLength(50);
        builder.Property(i => i.Name).HasMaxLength(300);
        builder.Property(i => i.Description).HasMaxLength(2000);
        builder.Property(i => i.Numerator);            // >=1000 free text ⇒ text; bound in validator
        builder.Property(i => i.Denominator);
        builder.Property(i => i.Inclusions).HasMaxLength(2000);
        builder.Property(i => i.Exclusions).HasMaxLength(2000);
        builder.Property(i => i.DataSource).HasMaxLength(1000);
        builder.Property(i => i.Unit).HasMaxLength(50);
        builder.Property(i => i.RateFactor).HasPrecision(18, 4);
        builder.Property(i => i.Target).HasPrecision(18, 4);
        builder.Property(i => i.WarningThreshold).HasPrecision(18, 4);
        builder.Property(i => i.ActionThreshold).HasPrecision(18, 4);
        builder.Property(i => i.Frequency).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Direction).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(i => new { i.TenantId, i.Code }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Status });

        builder.OwnsMany(i => i.Measurements, m =>
        {
            m.ToTable("indicator_measurement", "qams");
            // Shadow tenant column stamped from the owner; the composite FK to the owner
            // makes a mismatched value impossible to persist. RLS reads it.
            m.Property<Guid>("TenantId");
            m.WithOwner().HasForeignKey("TenantId", "indicator_id");
            m.HasKey("TenantId", "Id");
            m.Property(x => x.Numerator).HasPrecision(18, 4);
            m.Property(x => x.Denominator).HasPrecision(18, 4);
            m.Property(x => x.Value).HasPrecision(18, 4);
            m.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            m.Property(x => x.Note);
            m.HasIndex("TenantId", "indicator_id", "Period").IsUnique();
        });

        builder.Ignore(i => i.DomainEvents);
    }
}
