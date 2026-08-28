using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.RiskGovernance;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the FmeaStudy aggregate (HQMS M04). Tenant-first composite key,
/// enum-as-string columns fenced by CHECK domains in the migration, and an owned
/// failure-mode child carrying a shadow tenant column and composite ownership FK.
/// FORCE RLS on both tables is added in the migration (EF does not generate it).
/// </summary>
public sealed class FmeaStudyConfiguration : IEntityTypeConfiguration<FmeaStudy>
{
    public void Configure(EntityTypeBuilder<FmeaStudy> builder)
    {
        builder.ToTable("fmea_study", "qams");
        builder.HasKey(f => new { f.TenantId, f.Id });

        builder.Property(f => f.FmeaRef).HasMaxLength(30);
        builder.Property(f => f.Title).HasMaxLength(200);
        builder.Property(f => f.ProcessName).HasMaxLength(200);
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(f => new { f.TenantId, f.FmeaRef }).IsUnique()
            .HasDatabaseName("ux_fmea_study_tenant_id_fmea_ref");
        builder.HasIndex(f => new { f.TenantId, f.Status });

        builder.OwnsMany(f => f.FailureModes, mode =>
        {
            mode.ToTable("fmea_failure_mode", "qams");
            // Shadow tenant column stamped from the owner; the composite FK to the owner
            // makes a mismatched value impossible to persist. RLS reads it.
            mode.Property<Guid>("TenantId");
            mode.WithOwner().HasForeignKey("TenantId", "fmea_study_id");
            mode.HasKey("TenantId", "Id");
            mode.Property(x => x.ProcessStep).HasMaxLength(200);
            mode.Property(x => x.FailureModeText).HasMaxLength(500);
            mode.Property(x => x.Effect).HasMaxLength(1000);
            mode.Property(x => x.Cause).HasMaxLength(1000);
            mode.Property(x => x.RecommendedAction).HasMaxLength(2000);
            mode.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.Ignore(f => f.DomainEvents);
    }
}
