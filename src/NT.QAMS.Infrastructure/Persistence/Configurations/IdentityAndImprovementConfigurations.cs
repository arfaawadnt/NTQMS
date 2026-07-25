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

        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

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
        builder.Property(n => n.Description).HasMaxLength(4000);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.SourceType).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.RejectionReason).HasMaxLength(1000);

        builder.HasIndex(n => new { n.TenantId, n.NcRef }).IsUnique();
        builder.HasIndex(n => new { n.TenantId, n.Status });

        builder.OwnsMany(n => n.CapaActions, action =>
        {
            action.ToTable("capa_action", "qams");
            action.WithOwner().HasForeignKey("nc_id");
            action.HasKey(a => a.Id);
            action.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
            action.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            action.Property(a => a.Details).HasMaxLength(2000);
        });

        builder.OwnsMany(n => n.RcaRecords, rca =>
        {
            rca.ToTable("rca_record", "qams");
            rca.WithOwner().HasForeignKey("nc_id");
            rca.HasKey(r => r.Id);
            rca.Property(r => r.Method).HasConversion<string>().HasMaxLength(20);
            rca.Property(r => r.Analysis).HasMaxLength(8000);
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
        builder.Property(c => c.Description).HasMaxLength(4000);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ValidationVerdict).HasMaxLength(2000);
        builder.Property(c => c.InvestigationOutcome).HasMaxLength(4000);
        builder.Property(c => c.Resolution).HasMaxLength(4000);

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
