using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Domain.Organization;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branch", "qams");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Code).HasMaxLength(20);
        builder.Property(b => b.Name).HasMaxLength(200);
        builder.Property(b => b.City).HasMaxLength(100);
        builder.HasIndex(b => new { b.TenantId, b.Code }).IsUnique();
        builder.Ignore(b => b.DomainEvents);
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("department", "qams");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Code).HasMaxLength(20);
        builder.Property(d => d.Name).HasMaxLength(200);
        builder.HasIndex(d => new { d.TenantId, d.BranchId, d.Code }).IsUnique();
        builder.HasOne<Branch>().WithMany().HasForeignKey(d => d.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(d => d.DomainEvents);
    }
}

public sealed class TestCatalogItemConfiguration : IEntityTypeConfiguration<TestCatalogItem>
{
    public void Configure(EntityTypeBuilder<TestCatalogItem> builder)
    {
        builder.ToTable("test_catalog_item", "qams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TestCode).HasMaxLength(30);
        builder.Property(t => t.TestName).HasMaxLength(200);
        builder.Property(t => t.Methodology).HasMaxLength(300);
        builder.HasIndex(t => new { t.TenantId, t.TestCode }).IsUnique();
        builder.Ignore(t => t.DomainEvents);
    }
}

public sealed class LovEntryConfiguration : IEntityTypeConfiguration<LovEntry>
{
    public void Configure(EntityTypeBuilder<LovEntry> builder)
    {
        builder.ToTable("lov_entry", "qams");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Category).HasMaxLength(50);
        builder.Property(l => l.Code).HasMaxLength(50);
        builder.HasIndex(l => new { l.TenantId, l.Category, l.Code }).IsUnique();

        // Trilingual columns per the DB architecture decision (indexable; en is the anchor).
        builder.OwnsOne(l => l.Name, name =>
        {
            name.Property(n => n.En).HasColumnName("name_en").HasMaxLength(200);
            name.Property(n => n.Ar).HasColumnName("name_ar").HasMaxLength(200);
            name.Property(n => n.Fr).HasColumnName("name_fr").HasMaxLength(200);
        });

        builder.Ignore(l => l.DomainEvents);
    }
}

public sealed class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    public void Configure(EntityTypeBuilder<NotificationRule> builder)
    {
        builder.ToTable("notification_rule", "qams");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.EventKey).HasMaxLength(50);
        builder.Property(r => r.RecipientRoles).HasMaxLength(300);
        builder.Property(r => r.SubjectTemplate).HasMaxLength(300);
        builder.Property(r => r.BodyTemplate).HasMaxLength(4000);
        builder.HasIndex(r => new { r.TenantId, r.EventKey });
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
    {
        builder.ToTable("notification_dispatch", "qams");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.EventKey).HasMaxLength(50);
        builder.Property(d => d.RecipientEmail).HasMaxLength(320);
        builder.Property(d => d.Subject).HasMaxLength(400);
        builder.Property(d => d.Body).HasMaxLength(8000);
        builder.Property(d => d.EmailStatus).HasConversion<string>().HasMaxLength(10);
        builder.Property(d => d.Error).HasMaxLength(1500);

        builder.HasIndex(d => d.SourceEventId);
        builder.HasIndex(d => new { d.TenantId, d.RecipientUserId, d.ReadByRecipient });

        builder.Ignore(d => d.DomainEvents);
    }
}
