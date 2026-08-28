using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.SupplierQuality;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class RiskItemConfiguration : IEntityTypeConfiguration<RiskItem>
{
    public void Configure(EntityTypeBuilder<RiskItem> builder)
    {
        builder.ToTable("risk_item", "qams");
        builder.HasKey(r => new { r.TenantId, r.Id });
        builder.Property(r => r.RiskRef).HasMaxLength(30);
        builder.Property(r => r.Title).HasMaxLength(300);
        builder.Property(r => r.Category).HasMaxLength(50);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.TenantId, r.RiskRef }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Status });

        builder.OwnsMany(r => r.Actions, a =>
        {
            a.ToTable("mitigation_action", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            a.Property<Guid>("TenantId");
            a.WithOwner().HasForeignKey("TenantId", "risk_id");
            a.HasKey("TenantId", "Id");
            a.Property(x => x.Description);
        });

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("change_request", "qams");
        builder.HasKey(c => new { c.TenantId, c.Id });
        builder.Property(c => c.ChangeRef).HasMaxLength(30);
        builder.Property(c => c.Title).HasMaxLength(300);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.ImpactLevel).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(c => new { c.TenantId, c.ChangeRef }).IsUnique();
        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class ManagementReviewConfiguration : IEntityTypeConfiguration<ManagementReview>
{
    public void Configure(EntityTypeBuilder<ManagementReview> builder)
    {
        builder.ToTable("management_review", "qams");
        builder.HasKey(r => new { r.TenantId, r.Id });
        builder.Property(r => r.ReviewRef).HasMaxLength(30);
        builder.Property(r => r.Title).HasMaxLength(300);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.TenantId, r.ReviewRef }).IsUnique();

        // Free text; the 500 bound on the link and 10000 on the agenda live in
        // the command validator per schema hardening 1.2.
        builder.Property(r => r.MeetingLink).HasMaxLength(500);

        builder.OwnsMany(r => r.Decisions, d =>
        {
            d.ToTable("review_decision", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            d.Property<Guid>("TenantId");
            d.WithOwner().HasForeignKey("TenantId", "review_id");
            d.HasKey("TenantId", "Id");
            d.Property(x => x.Description);
        });

        builder.OwnsMany(r => r.ParticipantUsers, p =>
        {
            p.ToTable("review_participant", "qams");
            p.Property<Guid>("TenantId");
            p.WithOwner().HasForeignKey("TenantId", "review_id");
            p.HasKey("TenantId", "Id");
            // One row per user per review — a duplicate invite is a data error.
            p.HasIndex("TenantId", "review_id", "UserId")
                .IsUnique()
                .HasDatabaseName("ux_review_participant_user");
        });

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("supplier", "qams");
        builder.HasKey(s => new { s.TenantId, s.Id });
        builder.Property(s => s.SupplierRef).HasMaxLength(30);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.SupplierType).HasMaxLength(50);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(25);
        builder.Property(s => s.SuspensionReason).HasMaxLength(500);
        builder.Property(s => s.ServiceScope).HasMaxLength(300);
        builder.HasIndex(s => new { s.TenantId, s.SupplierRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.OwnsMany(s => s.Certificates, c =>
        {
            c.ToTable("supplier_certificate", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            c.Property<Guid>("TenantId");
            c.WithOwner().HasForeignKey("TenantId", "supplier_id");
            c.HasKey("TenantId", "Id");
            c.Property(x => x.CertificateType).HasMaxLength(100);
        });

        builder.OwnsMany(s => s.Contracts, con =>
        {
            con.ToTable("supplier_contract", "qams");
            con.Property<Guid>("TenantId");
            con.WithOwner().HasForeignKey("TenantId", "supplier_id");
            con.HasKey("TenantId", "Id");
            con.Property(x => x.ContractRef).HasMaxLength(60);
            con.Property(x => x.Title).HasMaxLength(300);
            con.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.OwnsMany(s => s.Cars, car =>
        {
            car.ToTable("supplier_car", "qams");
            car.Property<Guid>("TenantId");
            car.WithOwner().HasForeignKey("TenantId", "supplier_id");
            car.HasKey("TenantId", "Id");
            car.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.Ignore(s => s.OpenCarCount);
        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class SupplierEvaluationConfiguration : IEntityTypeConfiguration<SupplierEvaluation>
{
    public void Configure(EntityTypeBuilder<SupplierEvaluation> builder)
    {
        builder.ToTable("supplier_evaluation", "qams");
        builder.HasKey(e => new { e.TenantId, e.Id });
        // jsonb (schema hardening 1.3): the DB validates and indexes the document;
        // the domain keeps a string and owns serialization.
        builder.Property(e => e.Criteria).HasColumnType("jsonb");
        builder.Property(e => e.WeightedTotal).HasPrecision(5, 2);
        builder.HasIndex(e => new { e.TenantId, e.SupplierId });
        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class ConflictDeclarationConfiguration : IEntityTypeConfiguration<ConflictDeclaration>
{
    public void Configure(EntityTypeBuilder<ConflictDeclaration> builder)
    {
        builder.ToTable("conflict_declaration", "qams");
        builder.HasKey(c => new { c.TenantId, c.Id });
        builder.Property(c => c.ConflictRef).HasMaxLength(30);
        builder.Property(c => c.RelatedParty).HasMaxLength(300);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.RiskLevel).HasConversion<string>().HasMaxLength(10);
        builder.Property(c => c.Outcome).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => new { c.TenantId, c.ConflictRef }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.Ignore(c => c.DomainEvents);
    }
}
