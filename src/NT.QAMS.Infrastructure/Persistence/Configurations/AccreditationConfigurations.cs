using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Accreditation;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the StandardSet aggregate (HQMS M07). Tenant-first composite key,
/// enum-as-string columns fenced by CHECK domains in the migration, and an owned element
/// child carrying a shadow tenant column and composite ownership FK. FORCE RLS on both
/// tables is added in the migration (EF does not generate it).
/// </summary>
public sealed class StandardSetConfiguration : IEntityTypeConfiguration<StandardSet>
{
    public void Configure(EntityTypeBuilder<StandardSet> builder)
    {
        builder.ToTable("standard_set", "qams");
        builder.HasKey(s => new { s.TenantId, s.Id });

        builder.Property(s => s.Framework).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Version).HasMaxLength(40);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.OwnsMany(s => s.Elements, el =>
        {
            el.ToTable("standard_element", "qams");
            // Shadow tenant column stamped from the owner; the composite FK to the owner
            // makes a mismatched value impossible to persist. RLS reads it.
            el.Property<Guid>("TenantId");
            el.WithOwner().HasForeignKey("TenantId", "standard_set_id");
            el.HasKey("TenantId", "Id");
            el.Property(x => x.ChapterCode).HasMaxLength(40);
            el.Property(x => x.ChapterTitle).HasMaxLength(300);
            el.Property(x => x.StandardCode).HasMaxLength(40);
            el.Property(x => x.ElementCode).HasMaxLength(40);
            el.Property(x => x.Text);
            el.Property(x => x.AssessmentNote);
            el.Property(x => x.ComplianceStatus).HasConversion<string>().HasMaxLength(20);
            el.HasIndex("TenantId", "standard_set_id", "ElementCode")
                .IsUnique()
                .HasDatabaseName("ux_standard_element_set_code");
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

/// <summary>EF mapping for the EvidenceLink aggregate (HQMS M07).</summary>
public sealed class EvidenceLinkConfiguration : IEntityTypeConfiguration<EvidenceLink>
{
    public void Configure(EntityTypeBuilder<EvidenceLink> builder)
    {
        builder.ToTable("evidence_link", "qams");
        builder.HasKey(l => new { l.TenantId, l.Id });

        builder.Property(l => l.SourceType).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.SourceRef).HasMaxLength(200);

        builder.HasIndex(l => new { l.TenantId, l.StandardSetId });
        builder.HasIndex(l => new { l.TenantId, l.ElementId });

        builder.Ignore(l => l.DomainEvents);
    }
}
