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
        builder.HasKey(e => new { e.TenantId, e.Id });

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
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            cal.Property<Guid>("TenantId");
            cal.WithOwner().HasForeignKey("TenantId", "equipment_id");
            cal.HasKey("TenantId", "Id");
            cal.Property(c => c.Provider).HasMaxLength(200);
            cal.Property(c => c.Result).HasMaxLength(500);
        });

        builder.OwnsMany(e => e.Maintenance, m =>
        {
            m.ToTable("maintenance_record", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            m.Property<Guid>("TenantId");
            m.WithOwner().HasForeignKey("TenantId", "equipment_id");
            m.HasKey("TenantId", "Id");
            m.Property(x => x.WorkDescription);
        });

        builder.OwnsMany(e => e.IntermediateChecks, check =>
        {
            check.ToTable("intermediate_check", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            check.Property<Guid>("TenantId");
            check.WithOwner().HasForeignKey("TenantId", "equipment_id");
            check.HasKey("TenantId", "Id");
            check.Property(x => x.CheckType).HasMaxLength(200);
            check.Property(x => x.Remarks);
        });

        builder.OwnsMany(e => e.Downtime, d =>
        {
            d.ToTable("equipment_downtime", "qams");
            d.Property<Guid>("TenantId");
            d.WithOwner().HasForeignKey("TenantId", "equipment_id");
            d.HasKey("TenantId", "Id");
            d.Property(x => x.Reason).HasMaxLength(1000);
            d.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            d.Ignore(x => x.IsOpen);
        });

        builder.OwnsMany(e => e.SafetyNotices, sn =>
        {
            sn.ToTable("equipment_safety_notice", "qams");
            sn.Property<Guid>("TenantId");
            sn.WithOwner().HasForeignKey("TenantId", "equipment_id")
                .HasConstraintName("fk_eq_safety_notice_equipment_item_tenant_id_equipment_id");
            sn.HasKey("TenantId", "Id");
            sn.Property(x => x.Reference).HasMaxLength(100);
            sn.Property(x => x.Issuer).HasMaxLength(200);
            sn.Property(x => x.ActionNote).HasMaxLength(2000);
            sn.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            sn.Property(x => x.Severity).HasConversion<string>().HasMaxLength(10);
            sn.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class ReferenceStandardConfiguration : IEntityTypeConfiguration<ReferenceStandard>
{
    public void Configure(EntityTypeBuilder<ReferenceStandard> builder)
    {
        builder.ToTable("reference_standard", "qams");
        builder.HasKey(s => new { s.TenantId, s.Id });

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

        builder.HasIndex(s => new { s.TenantId, s.StandardRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class TestAuthorizationConfiguration : IEntityTypeConfiguration<TestAuthorization>
{
    public void Configure(EntityTypeBuilder<TestAuthorization> builder)
    {
        builder.ToTable("test_authorization", "qams");
        builder.HasKey(a => new { a.TenantId, a.Id });

        builder.Property(a => a.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(a => new { a.TenantId, a.UserId });
        builder.HasIndex(a => new { a.TenantId, a.TestCatalogItemId });
        builder.HasIndex(a => new { a.TenantId, a.Status });
        builder.HasIndex(a => a.CompetencyRecordId);

        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class CompetencyRecordConfiguration : IEntityTypeConfiguration<CompetencyRecord>
{
    public void Configure(EntityTypeBuilder<CompetencyRecord> builder)
    {
        builder.ToTable("competency_record", "qams");
        builder.HasKey(c => new { c.TenantId, c.Id });

        builder.Property(c => c.Subject).HasMaxLength(300);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.TraineeId });
        builder.HasIndex(c => new { c.TenantId, c.Status });

        builder.OwnsMany(c => c.Assessments, a =>
        {
            a.ToTable("assessment_result", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            a.Property<Guid>("TenantId");
            a.WithOwner().HasForeignKey("TenantId", "competency_id");
            a.HasKey("TenantId", "Id");
        });

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class TrainingAssignmentConfiguration : IEntityTypeConfiguration<TrainingAssignment>
{
    public void Configure(EntityTypeBuilder<TrainingAssignment> builder)
    {
        builder.ToTable("training_assignment", "qams");
        builder.HasKey(t => new { t.TenantId, t.Id });

        builder.Property(t => t.Subject).HasMaxLength(300);

        builder.HasIndex(t => new { t.TenantId, t.TraineeId, t.Completed });

        builder.Ignore(t => t.DomainEvents);
    }
}
