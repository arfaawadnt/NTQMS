using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.PatientExperience;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the SatisfactionSurvey aggregate (HQMS M11). Tenant-first composite key,
/// enum CHECK domain in the migration, and an owned question child with a shadow tenant
/// column and composite ownership FK. FORCE RLS is added in the migration.
/// </summary>
public sealed class SatisfactionSurveyConfiguration : IEntityTypeConfiguration<SatisfactionSurvey>
{
    public void Configure(EntityTypeBuilder<SatisfactionSurvey> builder)
    {
        builder.ToTable("satisfaction_survey", "qams");
        builder.HasKey(s => new { s.TenantId, s.Id });

        builder.Property(s => s.Title).HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.OwnsMany(s => s.Questions, q =>
        {
            q.ToTable("survey_question", "qams");
            q.Property<Guid>("TenantId");
            q.WithOwner().HasForeignKey("TenantId", "survey_id");
            q.HasKey("TenantId", "Id");
            q.Property(x => x.Text).HasMaxLength(500);
            q.Property(x => x.Domain).HasMaxLength(100);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

/// <summary>
/// EF mapping for the SurveyResponse aggregate (HQMS M11). Owned answer child with shadow
/// tenant column and composite ownership FK. FORCE RLS is added in the migration.
/// </summary>
public sealed class SurveyResponseConfiguration : IEntityTypeConfiguration<SurveyResponse>
{
    public void Configure(EntityTypeBuilder<SurveyResponse> builder)
    {
        builder.ToTable("survey_response", "qams");
        builder.HasKey(r => new { r.TenantId, r.Id });

        builder.Property(r => r.ServiceLine).HasMaxLength(150);

        builder.HasIndex(r => new { r.TenantId, r.SurveyId });

        builder.OwnsMany(r => r.Answers, a =>
        {
            a.ToTable("survey_answer", "qams");
            a.Property<Guid>("TenantId");
            a.WithOwner().HasForeignKey("TenantId", "survey_response_id");
            a.HasKey("TenantId", "Id");
        });

        builder.Ignore(r => r.DomainEvents);
    }
}
