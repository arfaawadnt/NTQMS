using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.PatientSafety;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the PatientSafetyEvent aggregate (HQMS M08). Tenant-first composite key,
/// enum-as-string columns fenced by CHECK domains in the migration. FORCE RLS in the migration.
/// </summary>
public sealed class PatientSafetyEventConfiguration : IEntityTypeConfiguration<PatientSafetyEvent>
{
    public void Configure(EntityTypeBuilder<PatientSafetyEvent> builder)
    {
        builder.ToTable("patient_safety_event", "qams");
        builder.HasKey(e => new { e.TenantId, e.Id });

        builder.Property(e => e.EventRef).HasMaxLength(30);
        builder.Property(e => e.PatientRef).HasMaxLength(100);
        builder.Property(e => e.Unit).HasMaxLength(100);
        builder.Property(e => e.Description);
        builder.Property(e => e.ReviewNotes);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.HarmLevel).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Origin).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Stage).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.EventRef }).IsUnique()
            .HasDatabaseName("ux_patient_safety_event_tenant_id_event_ref");
        builder.HasIndex(e => new { e.TenantId, e.Type, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.OccurredAtUtc });

        builder.Ignore(e => e.IsHospitalAcquiredPressureInjury);
        builder.Ignore(e => e.DomainEvents);
    }
}
