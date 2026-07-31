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
        // Tenant-first key (schema hardening Phase 5): this ledger is the first
        // partitioning target (HASH on tenant_id), and a partitioned table needs
        // the partition key in its primary key. The hash-chain columns are
        // untouched.
        builder.HasKey(e => new { e.TenantId, e.Id });
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
        builder.HasKey(s => new { s.TenantId, s.Id });
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
        // Stored as PostgreSQL inet (schema hardening 1.1): the DB validates the
        // address; the CLR/wire type stays string so the API contract is unchanged.
        builder.Property(e => e.IpAddress)
            .HasConversion(v => System.Net.IPAddress.Parse(v!), v => v!.ToString())
            .HasColumnType("inet");
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
        builder.Property(f => f.Actor).HasMaxLength(300);
        builder.HasIndex(f => new { f.TenantId, f.EntityId });
        builder.HasIndex(f => f.OccurredAtUtc);
    }
}

public sealed class AuditTrailReviewConfiguration : IEntityTypeConfiguration<AuditTrailReview>
{
    public void Configure(EntityTypeBuilder<AuditTrailReview> builder)
    {
        builder.ToTable("audit_trail_review", "qams");
        builder.HasKey(r => new { r.TenantId, r.Id });
        builder.Property(r => r.ReviewRef).HasMaxLength(30);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.TenantId, r.ReviewRef }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Status });
        builder.Ignore(r => r.DomainEvents);
    }
}
