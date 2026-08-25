using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.IncidentReporting;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the Incident aggregate (HQMS M02). Tenant-first composite key,
/// enum-as-string columns fenced by CHECK domains in the migration, and two owned
/// child tables that carry a shadow tenant column and a composite ownership FK so a
/// child under another tenant's parent is structurally impossible. FORCE RLS on all
/// three tables is added in the migration (EF does not generate it).
/// </summary>
public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incident", "qams");
        builder.HasKey(i => new { i.TenantId, i.Id });

        builder.Property(i => i.IncidentRef).HasMaxLength(30);
        builder.Property(i => i.Title).HasMaxLength(300);
        builder.Property(i => i.Description);
        builder.Property(i => i.Location).HasMaxLength(200);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(i => i.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(i => i.HarmGrade).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.AnonymousReferenceHash).HasMaxLength(64);
        builder.Property(i => i.InvestigationSummary);
        builder.Property(i => i.RejectionReason).HasMaxLength(1000);
        builder.Property(i => i.ClosureSummary);

        builder.HasIndex(i => new { i.TenantId, i.IncidentRef }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.AnonymousReferenceHash });

        builder.OwnsMany(i => i.ContributingFactors, factor =>
        {
            factor.ToTable("incident_contributing_factor", "qams");
            // Shadow tenant column stamped from the owner; the composite FK to the
            // owner makes a mismatched value impossible to persist. RLS reads it.
            factor.Property<Guid>("TenantId");
            factor.WithOwner().HasForeignKey("TenantId", "incident_id");
            factor.HasKey("TenantId", "Id");
            factor.Property(f => f.Category).HasConversion<string>().HasMaxLength(20);
            factor.Property(f => f.Description);
        });

        builder.OwnsMany(i => i.Timeline, entry =>
        {
            entry.ToTable("incident_timeline_entry", "qams");
            entry.Property<Guid>("TenantId");
            entry.WithOwner().HasForeignKey("TenantId", "incident_id");
            entry.HasKey("TenantId", "Id");
            entry.Property(t => t.Note);
        });

        builder.Ignore(i => i.DomainEvents);
    }
}
