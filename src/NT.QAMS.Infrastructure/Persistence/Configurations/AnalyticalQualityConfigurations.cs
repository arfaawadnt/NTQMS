using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.AnalyticalQuality;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class QcProfileConfiguration : IEntityTypeConfiguration<QcProfile>
{
    public void Configure(EntityTypeBuilder<QcProfile> builder)
    {
        builder.ToTable("qc_profile", "qams");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Analyte).HasMaxLength(100);
        builder.Property(p => p.Instrument).HasMaxLength(100);
        builder.Property(p => p.ControlLot).HasMaxLength(60);
        builder.Property(p => p.TargetMean).HasPrecision(18, 6);
        builder.Property(p => p.TargetSd).HasPrecision(18, 6);
        builder.HasIndex(p => new { p.TenantId, p.Analyte, p.Instrument, p.ControlLot });
        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class QcRunConfiguration : IEntityTypeConfiguration<QcRun>
{
    public void Configure(EntityTypeBuilder<QcRun> builder)
    {
        builder.ToTable("qc_run", "qams");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Value).HasPrecision(18, 6);
        builder.Property(r => r.ZScore).HasPrecision(10, 3);
        builder.Property(r => r.Outcome).HasMaxLength(15);
        builder.Property(r => r.ViolatedRules).HasMaxLength(60);
        builder.Property(r => r.Operator).HasMaxLength(150);
        builder.Property(r => r.TroubleshootingNote).HasMaxLength(2000);
        // The hot query path: recent runs for a profile (Levey-Jennings window).
        builder.HasIndex(r => new { r.TenantId, r.ProfileId, r.MeasuredAtUtc });
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class ValidationStudyConfiguration : IEntityTypeConfiguration<ValidationStudy>
{
    public void Configure(EntityTypeBuilder<ValidationStudy> builder)
    {
        builder.ToTable("validation_study", "qams");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudyRef).HasMaxLength(30);
        builder.Property(s => s.Analyte).HasMaxLength(100);
        builder.Property(s => s.Protocol).HasMaxLength(30);
        builder.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.TotalAllowableError).HasPrecision(10, 3);
        builder.Property(s => s.MeanBias).HasPrecision(10, 3);
        builder.Property(s => s.Cv).HasPrecision(10, 3);
        builder.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();

        builder.OwnsMany(s => s.Replicates, r =>
        {
            r.ToTable("validation_replicate", "qams");
            r.WithOwner().HasForeignKey("study_id");
            r.HasKey(x => x.Id);
            r.Property(x => x.Level).HasMaxLength(30);
            r.Property(x => x.Measured).HasPrecision(18, 6);
            r.Property(x => x.Reference).HasPrecision(18, 6);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class PtEnrollmentConfiguration : IEntityTypeConfiguration<PtEnrollment>
{
    public void Configure(EntityTypeBuilder<PtEnrollment> builder)
    {
        builder.ToTable("pt_enrollment", "qams");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PtRef).HasMaxLength(30);
        builder.Property(p => p.Scheme).HasMaxLength(100);
        builder.Property(p => p.Analyte).HasMaxLength(100);
        builder.Property(p => p.Cycle).HasMaxLength(50);
        builder.Property(p => p.Performance).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.SubmittedValue).HasPrecision(18, 6);
        builder.Property(p => p.AssignedValue).HasPrecision(18, 6);
        builder.Property(p => p.StandardDeviation).HasPrecision(18, 6);
        builder.Property(p => p.ZScore).HasPrecision(10, 3);
        builder.HasIndex(p => new { p.TenantId, p.PtRef }).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}
