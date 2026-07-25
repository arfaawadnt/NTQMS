using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.Equipment;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class EquipmentItemConfiguration : IEntityTypeConfiguration<EquipmentItem>
{
    public void Configure(EntityTypeBuilder<EquipmentItem> builder)
    {
        builder.ToTable("equipment_item", "qams");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(30);
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.SerialNumber).HasMaxLength(100);
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.SerialNumber }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.Status });

        builder.OwnsMany(e => e.Calibrations, cal =>
        {
            cal.ToTable("calibration_record", "qams");
            cal.WithOwner().HasForeignKey("equipment_id");
            cal.HasKey(c => c.Id);
            cal.Property(c => c.Provider).HasMaxLength(200);
            cal.Property(c => c.Result).HasMaxLength(500);
        });

        builder.OwnsMany(e => e.Maintenance, m =>
        {
            m.ToTable("maintenance_record", "qams");
            m.WithOwner().HasForeignKey("equipment_id");
            m.HasKey(x => x.Id);
            m.Property(x => x.WorkDescription).HasMaxLength(2000);
        });

        builder.OwnsMany(e => e.IntermediateChecks, check =>
        {
            check.ToTable("intermediate_check", "qams");
            check.WithOwner().HasForeignKey("equipment_id");
            check.HasKey(x => x.Id);
            check.Property(x => x.CheckType).HasMaxLength(200);
            check.Property(x => x.Remarks).HasMaxLength(2000);
        });

        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class ReferenceStandardConfiguration : IEntityTypeConfiguration<ReferenceStandard>
{
    public void Configure(EntityTypeBuilder<ReferenceStandard> builder)
    {
        builder.ToTable("reference_standard", "qams");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StandardRef).HasMaxLength(30);
        builder.Property(s => s.Name).HasMaxLength(300);
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(s => s.TraceableTo).HasMaxLength(500);
        builder.Property(s => s.Manufacturer).HasMaxLength(200);
        builder.Property(s => s.LotNumber).HasMaxLength(100);
        builder.Property(s => s.CertificateNumber).HasMaxLength(100);
        builder.Property(s => s.CertifiedValue).HasMaxLength(200);
        builder.Property(s => s.UncertaintyStatement).HasMaxLength(200);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.QuarantineReason).HasMaxLength(1000);

        builder.HasIndex(s => new { s.TenantId, s.StandardRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class CompetencyRecordConfiguration : IEntityTypeConfiguration<CompetencyRecord>
{
    public void Configure(EntityTypeBuilder<CompetencyRecord> builder)
    {
        builder.ToTable("competency_record", "qams");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Subject).HasMaxLength(300);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.RevocationReason).HasMaxLength(1000);

        builder.HasIndex(c => new { c.TenantId, c.TraineeId });
        builder.HasIndex(c => new { c.TenantId, c.Status });

        builder.OwnsMany(c => c.Assessments, a =>
        {
            a.ToTable("assessment_result", "qams");
            a.WithOwner().HasForeignKey("competency_id");
            a.HasKey(x => x.Id);
        });

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class TrainingAssignmentConfiguration : IEntityTypeConfiguration<TrainingAssignment>
{
    public void Configure(EntityTypeBuilder<TrainingAssignment> builder)
    {
        builder.ToTable("training_assignment", "qams");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Subject).HasMaxLength(300);

        builder.HasIndex(t => new { t.TenantId, t.TraineeId, t.Completed });

        builder.Ignore(t => t.DomainEvents);
    }
}
