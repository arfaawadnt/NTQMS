using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.InfectionControl;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the HaiCase aggregate (HQMS M09). Tenant-first composite key, enum-as-string
/// columns fenced by CHECK domains in the migration. FORCE RLS in the migration.
/// </summary>
public sealed class HaiCaseConfiguration : IEntityTypeConfiguration<HaiCase>
{
    public void Configure(EntityTypeBuilder<HaiCase> builder)
    {
        builder.ToTable("hai_case", "qams");
        builder.HasKey(e => new { e.TenantId, e.Id });

        builder.Property(e => e.CaseRef).HasMaxLength(30);
        builder.Property(e => e.PatientRef).HasMaxLength(100);
        builder.Property(e => e.Unit).HasMaxLength(100);
        builder.Property(e => e.Organism).HasMaxLength(200);
        builder.Property(e => e.Description);
        builder.Property(e => e.ReviewNotes);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.CaseRef }).IsUnique()
            .HasDatabaseName("ux_hai_case_tenant_id_case_ref");
        builder.HasIndex(e => new { e.TenantId, e.Type, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.OnsetDateUtc });

        builder.Ignore(e => e.AssociatedDevice);
        builder.Ignore(e => e.DomainEvents);
    }
}

/// <summary>
/// EF mapping for the DeviceExposure aggregate (HQMS M09) — the device-day denominator.
/// FORCE RLS in the migration.
/// </summary>
public sealed class DeviceExposureConfiguration : IEntityTypeConfiguration<DeviceExposure>
{
    public void Configure(EntityTypeBuilder<DeviceExposure> builder)
    {
        builder.ToTable("device_exposure", "qams");
        builder.HasKey(e => new { e.TenantId, e.Id });

        builder.Property(e => e.PatientRef).HasMaxLength(100);
        builder.Property(e => e.Unit).HasMaxLength(100);
        builder.Property(e => e.DeviceType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.DeviceType, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.InsertedAtUtc });

        builder.Ignore(e => e.DomainEvents);
    }
}
