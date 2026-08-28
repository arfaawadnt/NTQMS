using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.MortalityReview;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the MortalityReview aggregate (HQMS M10). Tenant-first composite key, enum-as-string
/// columns fenced by CHECK domains in the migration. FORCE RLS in the migration.
/// </summary>
public sealed class MortalityReviewConfiguration : IEntityTypeConfiguration<MortalityReview>
{
    public void Configure(EntityTypeBuilder<MortalityReview> builder)
    {
        builder.ToTable("mortality_review", "qams");
        builder.HasKey(m => new { m.TenantId, m.Id });

        builder.Property(m => m.ReviewRef).HasMaxLength(30);
        builder.Property(m => m.PatientRef).HasMaxLength(100);
        builder.Property(m => m.Unit).HasMaxLength(100);
        builder.Property(m => m.PrimaryDiagnosis).HasMaxLength(300);
        builder.Property(m => m.ClassificationFindings);
        builder.Property(m => m.SecondReviewNotes);
        builder.Property(m => m.CommitteeLearnings);
        builder.Property(m => m.Classification).HasConversion<string>().HasMaxLength(30);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(m => new { m.TenantId, m.ReviewRef }).IsUnique()
            .HasDatabaseName("ux_mortality_review_tenant_id_review_ref");
        builder.HasIndex(m => new { m.TenantId, m.Status });
        builder.HasIndex(m => new { m.TenantId, m.DeathDateUtc });

        builder.Ignore(m => m.RequiresSecondReview);
        builder.Ignore(m => m.DomainEvents);
    }
}

/// <summary>
/// EF mapping for the ComplicationCase aggregate (HQMS M10) — the morbidity register.
/// FORCE RLS in the migration.
/// </summary>
public sealed class ComplicationCaseConfiguration : IEntityTypeConfiguration<ComplicationCase>
{
    public void Configure(EntityTypeBuilder<ComplicationCase> builder)
    {
        builder.ToTable("complication_case", "qams");
        builder.HasKey(c => new { c.TenantId, c.Id });

        builder.Property(c => c.CaseRef).HasMaxLength(30);
        builder.Property(c => c.PatientRef).HasMaxLength(100);
        builder.Property(c => c.Unit).HasMaxLength(100);
        builder.Property(c => c.Description);
        builder.Property(c => c.ReviewNotes);
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Severity).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.CaseRef }).IsUnique()
            .HasDatabaseName("ux_complication_case_tenant_id_case_ref");
        builder.HasIndex(c => new { c.TenantId, c.Type, c.Status });
        builder.HasIndex(c => new { c.TenantId, c.OccurredDateUtc });

        builder.Ignore(c => c.DomainEvents);
    }
}
