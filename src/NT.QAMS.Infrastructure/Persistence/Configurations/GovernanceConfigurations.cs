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
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RiskRef).HasMaxLength(30);
        builder.Property(r => r.Title).HasMaxLength(300);
        builder.Property(r => r.Category).HasMaxLength(50);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.TenantId, r.RiskRef }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Status });

        builder.OwnsMany(r => r.Actions, a =>
        {
            a.ToTable("mitigation_action", "qams");
            a.WithOwner().HasForeignKey("risk_id");
            a.HasKey(x => x.Id);
            a.Property(x => x.Description).HasMaxLength(2000);
        });

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("change_request", "qams");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ChangeRef).HasMaxLength(30);
        builder.Property(c => c.Title).HasMaxLength(300);
        builder.Property(c => c.ImpactAnalysis).HasMaxLength(4000);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.RejectionReason).HasMaxLength(1000);
        builder.Property(c => c.ImplementationNotes).HasMaxLength(4000);
        builder.HasIndex(c => new { c.TenantId, c.ChangeRef }).IsUnique();
        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class ManagementReviewConfiguration : IEntityTypeConfiguration<ManagementReview>
{
    public void Configure(EntityTypeBuilder<ManagementReview> builder)
    {
        builder.ToTable("management_review", "qams");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ReviewRef).HasMaxLength(30);
        builder.Property(r => r.Title).HasMaxLength(300);
        builder.Property(r => r.Participants).HasMaxLength(2000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Minutes).HasMaxLength(20000);
        builder.HasIndex(r => new { r.TenantId, r.ReviewRef }).IsUnique();

        builder.OwnsMany(r => r.Decisions, d =>
        {
            d.ToTable("review_decision", "qams");
            d.WithOwner().HasForeignKey("review_id");
            d.HasKey(x => x.Id);
            d.Property(x => x.Description).HasMaxLength(2000);
        });

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("supplier", "qams");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SupplierRef).HasMaxLength(30);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.SupplierType).HasMaxLength(50);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(25);
        builder.Property(s => s.SuspensionReason).HasMaxLength(500);
        builder.HasIndex(s => new { s.TenantId, s.SupplierRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.OwnsMany(s => s.Certificates, c =>
        {
            c.ToTable("supplier_certificate", "qams");
            c.WithOwner().HasForeignKey("supplier_id");
            c.HasKey(x => x.Id);
            c.Property(x => x.CertificateType).HasMaxLength(100);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class SupplierEvaluationConfiguration : IEntityTypeConfiguration<SupplierEvaluation>
{
    public void Configure(EntityTypeBuilder<SupplierEvaluation> builder)
    {
        builder.ToTable("supplier_evaluation", "qams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CriteriaJson).HasMaxLength(8000);
        builder.Property(e => e.WeightedTotal).HasPrecision(5, 2);
        builder.HasIndex(e => new { e.TenantId, e.SupplierId });
        builder.Ignore(e => e.DomainEvents);
    }
}
