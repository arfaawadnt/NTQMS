using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.AuditManagement;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class AuditConfiguration : IEntityTypeConfiguration<Audit>
{
    public void Configure(EntityTypeBuilder<Audit> builder)
    {
        builder.ToTable("audit", "qams");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AuditRef).HasMaxLength(30);
        builder.Property(a => a.Title).HasMaxLength(300);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(a => new { a.TenantId, a.AuditRef }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.Status });

        builder.OwnsMany(a => a.Checklist, item =>
        {
            item.ToTable("audit_checklist_item", "qams");
            item.WithOwner().HasForeignKey("audit_id");
            item.HasKey(i => i.Id);
            item.Property(i => i.IsoClause).HasMaxLength(30);
            item.Property(i => i.Question).HasMaxLength(1000);
            item.Property(i => i.Verdict).HasConversion<string>().HasMaxLength(20);
            item.Property(i => i.Evidence).HasMaxLength(2000);
        });

        builder.OwnsMany(a => a.Findings, finding =>
        {
            finding.ToTable("audit_finding", "qams");
            finding.WithOwner().HasForeignKey("audit_id");
            finding.HasKey(f => f.Id);
            finding.Property(f => f.Grade).HasConversion<string>().HasMaxLength(20);
            finding.Property(f => f.Description).HasMaxLength(4000);
        });

        builder.Ignore(a => a.DomainEvents);
    }
}
