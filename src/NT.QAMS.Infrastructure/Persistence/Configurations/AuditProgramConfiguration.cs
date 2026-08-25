using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.AuditManagement;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the AuditProgram aggregate (HQMS M05). Tenant-first composite key,
/// enum-as-string columns fenced by CHECK domains in the migration, and an owned plan-line
/// child carrying a shadow tenant column and composite ownership FK. FORCE RLS on both
/// tables is added in the migration (EF does not generate it).
/// </summary>
public sealed class AuditProgramConfiguration : IEntityTypeConfiguration<AuditProgram>
{
    public void Configure(EntityTypeBuilder<AuditProgram> builder)
    {
        builder.ToTable("audit_program", "qams");
        builder.HasKey(p => new { p.TenantId, p.Id });

        builder.Property(p => p.Title).HasMaxLength(200);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(p => new { p.TenantId, p.Year });

        builder.OwnsMany(p => p.Plan, line =>
        {
            line.ToTable("planned_audit", "qams");
            // Shadow tenant column stamped from the owner; the composite FK to the owner
            // makes a mismatched value impossible to persist. RLS reads it.
            line.Property<Guid>("TenantId");
            line.WithOwner().HasForeignKey("TenantId", "audit_program_id");
            line.HasKey("TenantId", "Id");
            line.Property(x => x.ScopeArea).HasMaxLength(200);
            line.Property(x => x.StandardChapter).HasMaxLength(120);
            line.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            line.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.Ignore(p => p.DomainEvents);
    }
}
