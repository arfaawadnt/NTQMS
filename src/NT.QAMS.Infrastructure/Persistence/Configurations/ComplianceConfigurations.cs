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
