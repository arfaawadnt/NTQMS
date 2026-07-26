using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.ComplianceLedger;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

// These ledgers live in the audit schema and are append-only. Production
// grants qams_app INSERT/SELECT only (no UPDATE/DELETE) — the migration adds a
// guard trigger; RLS scopes reads by tenant.

public sealed class AuditTrailEntryConfiguration : IEntityTypeConfiguration<AuditTrailEntry>
{
    public void Configure(EntityTypeBuilder<AuditTrailEntry> builder)
    {
        builder.ToTable("audit_trail", "audit");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasMaxLength(400);
        builder.Property(e => e.PrevHash).HasMaxLength(64);
        builder.Property(e => e.EntryHash).HasMaxLength(64);
        builder.HasIndex(e => new { e.TenantId, e.Sequence }).IsUnique();
        builder.HasIndex(e => e.OccurredAtUtc);
    }
}

public sealed class SignatureRecordConfiguration : IEntityTypeConfiguration<SignatureRecord>
{
    public void Configure(EntityTypeBuilder<SignatureRecord> builder)
    {
        builder.ToTable("electronic_signature", "audit");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SignerDisplay).HasMaxLength(150);
        builder.Property(s => s.Meaning).HasMaxLength(500);
        builder.Property(s => s.SubjectRef).HasMaxLength(120);
        builder.Property(s => s.ContentHash).HasMaxLength(64);
        builder.HasIndex(s => new { s.TenantId, s.SignedAtUtc });
        builder.HasIndex(s => s.SubjectRef);
    }
}

public sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_event", "audit");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasMaxLength(40);
        builder.Property(e => e.Actor).HasMaxLength(320);
        builder.Property(e => e.IpAddress).HasMaxLength(60);
        builder.Property(e => e.Detail).HasMaxLength(500);
        builder.HasIndex(e => e.OccurredAtUtc);
    }
}

/// <summary>Field-level change ledger — append-only, protected by the same trigger family.</summary>
public sealed class FieldChangeRecordConfiguration : IEntityTypeConfiguration<FieldChangeRecord>
{
    public void Configure(EntityTypeBuilder<FieldChangeRecord> builder)
    {
        builder.ToTable("field_change", "audit");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.EntityType).HasMaxLength(150);
        builder.Property(f => f.EntityId).HasMaxLength(200);
        builder.Property(f => f.Action).HasMaxLength(20);
        builder.Property(f => f.Property).HasMaxLength(150);
        builder.Property(f => f.OldValue).HasMaxLength(4000);
        builder.Property(f => f.NewValue).HasMaxLength(4000);
        builder.Property(f => f.Actor).HasMaxLength(300);
        builder.Property(f => f.Reason).HasMaxLength(1000);
        builder.HasIndex(f => new { f.TenantId, f.EntityId });
        builder.HasIndex(f => f.OccurredAtUtc);
    }
}

public sealed class AuditTrailReviewConfiguration : IEntityTypeConfiguration<AuditTrailReview>
{
    public void Configure(EntityTypeBuilder<AuditTrailReview> builder)
    {
        builder.ToTable("audit_trail_review", "qams");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ReviewRef).HasMaxLength(30);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Conclusion).HasMaxLength(4000);
        builder.HasIndex(r => new { r.TenantId, r.ReviewRef }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Status });
        builder.Ignore(r => r.DomainEvents);
    }
}
