using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_account", "qams");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.DisplayName).HasMaxLength(150);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(30);

        builder.Property(u => u.PreferredLanguage).HasMaxLength(10);

        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        builder.HasIndex(u => u.RoleId);

        // The working scope. Owned collections, so a user's scope is loaded and
        // replaced as one unit with the account it belongs to.
        builder.OwnsMany(u => u.BranchAccess, access =>
        {
            access.ToTable("user_branch_access", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            access.Property<Guid>("TenantId");
            access.WithOwner().HasForeignKey("user_id");
            access.HasKey("user_id", nameof(UserBranchAccess.BranchId));
        });

        builder.OwnsMany(u => u.DepartmentAccess, access =>
        {
            access.ToTable("user_department_access", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            access.Property<Guid>("TenantId");
            access.WithOwner().HasForeignKey("user_id");
            access.HasKey("user_id", nameof(UserDepartmentAccess.DepartmentId));
        });

        builder.Ignore(u => u.HasUnrestrictedBranchAccess);
        builder.Ignore(u => u.HasUnrestrictedDepartmentAccess);
        builder.Ignore(u => u.DomainEvents);
    }
}

public sealed class NonconformanceConfiguration : IEntityTypeConfiguration<Nonconformance>
{
    public void Configure(EntityTypeBuilder<Nonconformance> builder)
    {
        builder.ToTable("nonconformance", "qams");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.NcRef).HasMaxLength(30);
        builder.Property(n => n.Title).HasMaxLength(300);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.SourceType).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.EventType).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(n => new { n.TenantId, n.NcRef }).IsUnique();
        builder.HasIndex(n => new { n.TenantId, n.Status });

        builder.OwnsMany(n => n.CapaActions, action =>
        {
            action.ToTable("capa_action", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            action.Property<Guid>("TenantId");
            action.WithOwner().HasForeignKey("nc_id");
            action.HasKey(a => a.Id);
            action.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
            action.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            action.Property(a => a.Details);
        });

        builder.OwnsMany(n => n.RcaRecords, rca =>
        {
            rca.ToTable("rca_record", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            rca.Property<Guid>("TenantId");
            rca.WithOwner().HasForeignKey("nc_id");
            rca.HasKey(r => r.Id);
            rca.Property(r => r.Method).HasConversion<string>().HasMaxLength(20);
            rca.Property(r => r.Analysis);
        });

        builder.Ignore(n => n.DomainEvents);
    }
}

public sealed class RefCounterConfiguration : IEntityTypeConfiguration<RefCounter>
{
    public void Configure(EntityTypeBuilder<RefCounter> builder)
    {
        builder.ToTable("ref_counter", "qams");
        builder.HasKey(c => new { c.TenantId, c.RefType, c.Year });
        builder.Property(c => c.RefType).HasMaxLength(10);
    }
}

/// <summary>Customer complaint (Improvement context) — tenant-scoped, ref unique per tenant.</summary>
public sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.ToTable("complaint", "qams");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ComplaintRef).HasMaxLength(30);
        builder.Property(c => c.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ComplainantName).HasMaxLength(300);
        builder.Property(c => c.ComplainantContact).HasMaxLength(300);
        builder.Property(c => c.Subject).HasMaxLength(300);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.ComplaintRef }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Status });
    }
}

/// <summary>Retired password hashes for the reuse ban (saas schema, per-user).</summary>
public sealed class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
    {
        builder.ToTable("password_history", "saas");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.PasswordHash).HasMaxLength(500);
        builder.HasIndex(h => new { h.UserId, h.SetAtUtc });
    }
}

public sealed class QualityObjectiveConfiguration : IEntityTypeConfiguration<QualityObjective>
{
    public void Configure(EntityTypeBuilder<QualityObjective> builder)
    {
        builder.ToTable("quality_objective", "qams");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.ObjectiveRef).HasMaxLength(30);
        builder.Property(o => o.Title).HasMaxLength(300);
        builder.Property(o => o.Metric).HasMaxLength(300);
        builder.Property(o => o.Unit).HasMaxLength(30);
        builder.Property(o => o.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(o => new { o.TenantId, o.ObjectiveRef }).IsUnique();
        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.Ignore(o => o.CurrentValue);
        builder.Ignore(o => o.OnTarget);

        builder.OwnsMany(o => o.Updates, update =>
        {
            update.ToTable("objective_progress", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            update.Property<Guid>("TenantId");
            update.WithOwner().HasForeignKey("objective_id");
            update.HasKey(u => u.Id);
            update.Property(u => u.Comment);
        });

        builder.Ignore(o => o.DomainEvents);
    }
}

public sealed class UserAccessReviewConfiguration : IEntityTypeConfiguration<UserAccessReview>
{
    public void Configure(EntityTypeBuilder<UserAccessReview> builder)
    {
        builder.ToTable("user_access_review", "qams");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ReviewRef).HasMaxLength(30);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.TenantId, r.ReviewRef }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Status });
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class QualityPolicyConfiguration : IEntityTypeConfiguration<QualityPolicy>
{
    public void Configure(EntityTypeBuilder<QualityPolicy> builder)
    {
        builder.ToTable("quality_policy", "qams");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PolicyRef).HasMaxLength(30);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(p => new { p.TenantId, p.PolicyRef }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Version }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Status });
        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class FeedbackEntryConfiguration : IEntityTypeConfiguration<FeedbackEntry>
{
    public void Configure(EntityTypeBuilder<FeedbackEntry> builder)
    {
        builder.ToTable("feedback_entry", "qams");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.FeedbackRef).HasMaxLength(30);
        builder.Property(f => f.Source).HasMaxLength(100);
        builder.Property(f => f.Channel).HasMaxLength(100);
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Subject).HasMaxLength(300);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(f => new { f.TenantId, f.FeedbackRef }).IsUnique();
        builder.HasIndex(f => new { f.TenantId, f.Status });
        builder.Ignore(f => f.DomainEvents);
    }
}
