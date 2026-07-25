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

/// <summary>MU budget with owned components — ref unique per tenant.</summary>
public sealed class UncertaintyBudgetConfiguration : IEntityTypeConfiguration<UncertaintyBudget>
{
    public void Configure(EntityTypeBuilder<UncertaintyBudget> builder)
    {
        builder.ToTable("uncertainty_budget", "qams");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.BudgetRef).HasMaxLength(30);
        builder.Property(b => b.Analyte).HasMaxLength(200);
        builder.Property(b => b.Method).HasMaxLength(300);
        builder.Property(b => b.Unit).HasMaxLength(50);
        builder.Property(b => b.Level).HasMaxLength(100);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(b => new { b.TenantId, b.BudgetRef }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.Status });

        builder.OwnsMany(b => b.Components, component =>
        {
            component.ToTable("uncertainty_component", "qams");
            component.WithOwner().HasForeignKey("budget_id");
            component.HasKey(c => c.Id);
            component.Property(c => c.Name).HasMaxLength(300);
            component.Property(c => c.Type).HasConversion<string>().HasMaxLength(10);
            component.Property(c => c.Source).HasMaxLength(500);
        });
    }
}

public sealed class PtPlanConfiguration : IEntityTypeConfiguration<PtPlan>
{
    public void Configure(EntityTypeBuilder<PtPlan> builder)
    {
        builder.ToTable("pt_plan", "qams");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PlanRef).HasMaxLength(30);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ClosureSummary).HasMaxLength(4000);
        builder.HasIndex(p => new { p.TenantId, p.PlanRef }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Year }).IsUnique();

        builder.OwnsMany(p => p.Items, item =>
        {
            item.ToTable("pt_plan_item", "qams");
            item.WithOwner().HasForeignKey("plan_id");
            item.HasKey(i => i.Id);
            item.Property(i => i.Scheme).HasMaxLength(200);
            item.Property(i => i.Analyte).HasMaxLength(200);
            item.Property(i => i.Provider).HasMaxLength(200);
            item.Property(i => i.LastEnrollmentRef).HasMaxLength(30);
            item.Property(i => i.Notes).HasMaxLength(1000);
        });

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class MethodComparisonStudyConfiguration : IEntityTypeConfiguration<MethodComparisonStudy>
{
    public void Configure(EntityTypeBuilder<MethodComparisonStudy> builder)
    {
        builder.ToTable("method_comparison_study", "qams");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudyRef).HasMaxLength(30);
        builder.Property(s => s.Analyte).HasMaxLength(200);
        builder.Property(s => s.Unit).HasMaxLength(50);
        builder.Property(s => s.ReferenceMethod).HasMaxLength(200);
        builder.Property(s => s.TestMethod).HasMaxLength(200);
        builder.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.State });

        builder.OwnsMany(s => s.Pairs, pair =>
        {
            pair.ToTable("measurement_pair", "qams");
            pair.WithOwner().HasForeignKey("study_id");
            pair.HasKey(p => p.Id);
            pair.Property(p => p.SampleId).HasMaxLength(100);
        });

        builder.Ignore(s => s.MeetsRecommendedPower);
        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class LinearityStudyConfiguration : IEntityTypeConfiguration<LinearityStudy>
{
    public void Configure(EntityTypeBuilder<LinearityStudy> builder)
    {
        builder.ToTable("linearity_study", "qams");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudyRef).HasMaxLength(30);
        builder.Property(s => s.Analyte).HasMaxLength(200);
        builder.Property(s => s.Unit).HasMaxLength(50);
        builder.Property(s => s.Method).HasMaxLength(300);
        builder.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.State });

        builder.OwnsMany(s => s.Measurements, m =>
        {
            m.ToTable("linearity_measurement", "qams");
            m.WithOwner().HasForeignKey("study_id");
            m.HasKey(x => x.Id);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class DetectionLimitStudyConfiguration : IEntityTypeConfiguration<DetectionLimitStudy>
{
    public void Configure(EntityTypeBuilder<DetectionLimitStudy> builder)
    {
        builder.ToTable("detection_limit_study", "qams");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudyRef).HasMaxLength(30);
        builder.Property(s => s.Analyte).HasMaxLength(200);
        builder.Property(s => s.Unit).HasMaxLength(50);
        builder.Property(s => s.Method).HasMaxLength(300);
        builder.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.State });

        builder.OwnsMany(s => s.Measurements, m =>
        {
            m.ToTable("detection_measurement", "qams");
            m.WithOwner().HasForeignKey("study_id");
            m.HasKey(x => x.Id);
            m.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class ReferenceIntervalStudyConfiguration : IEntityTypeConfiguration<ReferenceIntervalStudy>
{
    public void Configure(EntityTypeBuilder<ReferenceIntervalStudy> builder)
    {
        builder.ToTable("reference_interval_study", "qams");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudyRef).HasMaxLength(30);
        builder.Property(s => s.Analyte).HasMaxLength(200);
        builder.Property(s => s.Unit).HasMaxLength(50);
        builder.Property(s => s.Population).HasMaxLength(150);
        builder.Property(s => s.Source).HasMaxLength(300);
        builder.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Verdict).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.State });

        builder.OwnsMany(s => s.Samples, x =>
        {
            x.ToTable("reference_sample", "qams");
            x.WithOwner().HasForeignKey("study_id");
            x.HasKey(p => p.Id);
            x.Property(p => p.SubjectRef).HasMaxLength(100);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class SigmaAssessmentConfiguration : IEntityTypeConfiguration<SigmaAssessment>
{
    public void Configure(EntityTypeBuilder<SigmaAssessment> builder)
    {
        builder.ToTable("sigma_assessment", "qams");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AssessmentRef).HasMaxLength(30);
        builder.Property(a => a.Analyte).HasMaxLength(200);
        builder.Property(a => a.Unit).HasMaxLength(50);
        builder.Property(a => a.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Grade).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => new { a.TenantId, a.AssessmentRef }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.State });
        builder.Ignore(a => a.QcRecommendation);
        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class PrecisionStudyConfiguration : IEntityTypeConfiguration<PrecisionStudy>
{
    public void Configure(EntityTypeBuilder<PrecisionStudy> builder)
    {
        builder.ToTable("precision_study", "qams");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudyRef).HasMaxLength(30);
        builder.Property(s => s.Analyte).HasMaxLength(200);
        builder.Property(s => s.Unit).HasMaxLength(50);
        builder.Property(s => s.Level).HasMaxLength(100);
        builder.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.State });

        builder.OwnsMany(s => s.Measurements, m =>
        {
            m.ToTable("precision_measurement", "qams");
            m.WithOwner().HasForeignKey("study_id");
            m.HasKey(x => x.Id);
            m.Property(x => x.RunLabel).HasMaxLength(60);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class OutlierScreeningConfiguration : IEntityTypeConfiguration<OutlierScreening>
{
    public void Configure(EntityTypeBuilder<OutlierScreening> b)
    {
        b.ToTable("outlier_screening", "qams");
        b.HasKey(s => s.Id);
        b.Property(s => s.ScreeningRef).HasMaxLength(30);
        b.Property(s => s.Dataset).HasMaxLength(200);
        b.Property(s => s.Unit).HasMaxLength(50);
        b.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(s => new { s.TenantId, s.ScreeningRef }).IsUnique();
        b.HasIndex(s => new { s.TenantId, s.State });
        b.OwnsMany(s => s.Points, p =>
        {
            p.ToTable("outlier_point", "qams");
            p.WithOwner().HasForeignKey("screening_id");
            p.HasKey(x => x.Id);
            p.Property(x => x.Label).HasMaxLength(100);
        });
        b.Ignore(s => s.DomainEvents);
    }
}

public sealed class CarryoverStudyConfiguration : IEntityTypeConfiguration<CarryoverStudy>
{
    public void Configure(EntityTypeBuilder<CarryoverStudy> b)
    {
        b.ToTable("carryover_study", "qams");
        b.HasKey(s => s.Id);
        b.Property(s => s.StudyRef).HasMaxLength(30);
        b.Property(s => s.Analyte).HasMaxLength(200);
        b.Property(s => s.Unit).HasMaxLength(50);
        b.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        b.HasIndex(s => new { s.TenantId, s.State });
        b.OwnsMany(s => s.Readings, r =>
        {
            r.ToTable("carryover_reading", "qams");
            r.WithOwner().HasForeignKey("study_id");
            r.HasKey(x => x.Id);
            r.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
        });
        b.Ignore(s => s.DomainEvents);
    }
}

public sealed class LotComparisonStudyConfiguration : IEntityTypeConfiguration<LotComparisonStudy>
{
    public void Configure(EntityTypeBuilder<LotComparisonStudy> b)
    {
        b.ToTable("lot_comparison_study", "qams");
        b.HasKey(s => s.Id);
        b.Property(s => s.StudyRef).HasMaxLength(30);
        b.Property(s => s.Analyte).HasMaxLength(200);
        b.Property(s => s.Unit).HasMaxLength(50);
        b.Property(s => s.CurrentLot).HasMaxLength(60);
        b.Property(s => s.NewLot).HasMaxLength(60);
        b.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        b.HasIndex(s => new { s.TenantId, s.State });
        b.OwnsMany(s => s.Pairs, p =>
        {
            p.ToTable("lot_sample_pair", "qams");
            p.WithOwner().HasForeignKey("study_id");
            p.HasKey(x => x.Id);
            p.Property(x => x.SampleId).HasMaxLength(100);
        });
        b.Ignore(s => s.DomainEvents);
    }
}

public sealed class InterferenceStudyConfiguration : IEntityTypeConfiguration<InterferenceStudy>
{
    public void Configure(EntityTypeBuilder<InterferenceStudy> b)
    {
        b.ToTable("interference_study", "qams");
        b.HasKey(s => s.Id);
        b.Property(s => s.StudyRef).HasMaxLength(30);
        b.Property(s => s.Analyte).HasMaxLength(200);
        b.Property(s => s.Unit).HasMaxLength(50);
        b.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        b.HasIndex(s => new { s.TenantId, s.State });
        b.OwnsMany(s => s.Measurements, m =>
        {
            m.ToTable("interference_measurement", "qams");
            m.WithOwner().HasForeignKey("study_id");
            m.HasKey(x => x.Id);
            m.Property(x => x.Interferent).HasMaxLength(120);
        });
        b.Ignore(s => s.DomainEvents);
    }
}

public sealed class InstrumentComparabilityStudyConfiguration : IEntityTypeConfiguration<InstrumentComparabilityStudy>
{
    public void Configure(EntityTypeBuilder<InstrumentComparabilityStudy> b)
    {
        b.ToTable("instrument_comparability_study", "qams");
        b.HasKey(s => s.Id);
        b.Property(s => s.StudyRef).HasMaxLength(30);
        b.Property(s => s.Analyte).HasMaxLength(200);
        b.Property(s => s.Unit).HasMaxLength(50);
        b.Property(s => s.ReferenceInstrument).HasMaxLength(100);
        b.Property(s => s.State).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(s => new { s.TenantId, s.StudyRef }).IsUnique();
        b.HasIndex(s => new { s.TenantId, s.State });
        b.OwnsMany(s => s.Readings, r =>
        {
            r.ToTable("instrument_reading", "qams");
            r.WithOwner().HasForeignKey("study_id");
            r.HasKey(x => x.Id);
            r.Property(x => x.Instrument).HasMaxLength(100);
            r.Property(x => x.SampleId).HasMaxLength(100);
        });
        b.Ignore(s => s.DomainEvents);
    }
}
