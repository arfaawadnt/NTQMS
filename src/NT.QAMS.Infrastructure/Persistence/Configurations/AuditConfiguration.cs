using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.AuditManagement;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class AuditConfiguration : IEntityTypeConfiguration<Audit>
{
    public void Configure(EntityTypeBuilder<Audit> builder)
    {
        builder.ToTable("audit", "qams");
        builder.HasKey(a => new { a.TenantId, a.Id });

        builder.Property(a => a.AuditRef).HasMaxLength(30);
        builder.Property(a => a.Title).HasMaxLength(300);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(a => new { a.TenantId, a.AuditRef }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.Status });

        builder.OwnsMany(a => a.Checklist, item =>
        {
            item.ToTable("audit_checklist_item", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            item.Property<Guid>("TenantId");
            item.WithOwner().HasForeignKey("TenantId", "audit_id");
            item.HasKey("TenantId", "Id");
            item.Property(i => i.IsoClause).HasMaxLength(30);
            item.Property(i => i.Question);
            item.Property(i => i.Verdict).HasConversion<string>().HasMaxLength(20);
            item.Property(i => i.Evidence);
        });

        builder.OwnsMany(a => a.Findings, finding =>
        {
            finding.ToTable("audit_finding", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            finding.Property<Guid>("TenantId");
            finding.WithOwner().HasForeignKey("TenantId", "audit_id");
            finding.HasKey("TenantId", "Id");
            finding.Property(f => f.Grade).HasConversion<string>().HasMaxLength(20);
            finding.Property(f => f.Description);
        });

        builder.Ignore(a => a.DomainEvents);
    }
}
