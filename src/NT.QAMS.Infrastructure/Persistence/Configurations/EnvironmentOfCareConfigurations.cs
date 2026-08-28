using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.EnvironmentOfCare;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the SafetyRound aggregate (HQMS M15) with its owned findings. The child carries a
/// shadow tenant_id and a composite FK to the round. FORCE RLS in the migration.
/// </summary>
public sealed class SafetyRoundConfiguration : IEntityTypeConfiguration<SafetyRound>
{
    public void Configure(EntityTypeBuilder<SafetyRound> builder)
    {
        builder.ToTable("safety_round", "qams");
        builder.HasKey(r => new { r.TenantId, r.Id });

        builder.Property(r => r.RoundRef).HasMaxLength(30);
        builder.Property(r => r.Area).HasMaxLength(150);
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(r => new { r.TenantId, r.RoundRef }).IsUnique()
            .HasDatabaseName("ux_safety_round_tenant_id_round_ref");
        builder.HasIndex(r => new { r.TenantId, r.Type, r.Status });
        builder.HasIndex(r => new { r.TenantId, r.ScheduledDate });

        builder.Ignore(r => r.OpenFindingCount);

        builder.OwnsMany(r => r.Findings, f =>
        {
            f.ToTable("safety_round_finding", "qams");
            f.Property<Guid>("TenantId");
            f.WithOwner().HasForeignKey("TenantId", "safety_round_id");
            f.HasKey("TenantId", "Id");
            f.Property(x => x.Description).HasMaxLength(2000);
            f.Property(x => x.CorrectiveNote).HasMaxLength(2000);
            f.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            f.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.Ignore(r => r.DomainEvents);
    }
}

/// <summary>EF mapping for the Drill aggregate (HQMS M15). FORCE RLS in the migration.</summary>
public sealed class DrillConfiguration : IEntityTypeConfiguration<Drill>
{
    public void Configure(EntityTypeBuilder<Drill> builder)
    {
        builder.ToTable("drill", "qams");
        builder.HasKey(d => new { d.TenantId, d.Id });

        builder.Property(d => d.DrillRef).HasMaxLength(30);
        builder.Property(d => d.Location).HasMaxLength(150);
        builder.Property(d => d.ImprovementNotes);
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(d => new { d.TenantId, d.DrillRef }).IsUnique()
            .HasDatabaseName("ux_drill_tenant_id_drill_ref");
        builder.HasIndex(d => new { d.TenantId, d.Type, d.Status });
        builder.HasIndex(d => new { d.TenantId, d.ScheduledDate });

        builder.Ignore(d => d.Effectiveness);
        builder.Ignore(d => d.DomainEvents);
    }
}
