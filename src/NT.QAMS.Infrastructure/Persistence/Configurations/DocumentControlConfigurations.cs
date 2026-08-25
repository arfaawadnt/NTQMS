using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Files;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class ControlledDocumentConfiguration : IEntityTypeConfiguration<ControlledDocument>
{
    public void Configure(EntityTypeBuilder<ControlledDocument> builder)
    {
        builder.ToTable("controlled_document", "qams");
        builder.HasKey(d => new { d.TenantId, d.Id });

        builder.Property(d => d.Code).HasMaxLength(40);
        builder.Property(d => d.Title).HasMaxLength(300);
        builder.Property(d => d.Category).HasMaxLength(50);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.AudienceScope).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(d => new { d.TenantId, d.Code }).IsUnique();
        builder.HasIndex(d => new { d.TenantId, d.Status });

        builder.OwnsMany(d => d.AudienceDepartments, aud =>
        {
            aud.ToTable("document_audience_department", "qams");
            // Shadow tenant column stamped from the owner; the composite FK to the
            // owner makes a mismatched value impossible to persist. RLS reads it.
            aud.Property<Guid>("TenantId");
            aud.WithOwner().HasForeignKey("TenantId", "document_id");
            aud.HasKey("TenantId", "Id");
        });

        builder.OwnsMany(d => d.Versions, version =>
        {
            version.ToTable("document_version", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            version.Property<Guid>("TenantId");
            version.WithOwner().HasForeignKey("TenantId", "document_id");
            version.HasKey("TenantId", "Id");
            version.Property(v => v.State).HasConversion<string>().HasMaxLength(20);
            version.Property(v => v.ChangeSummary);
            version.Property(v => v.RejectionReason);
            version.Ignore(v => v.VersionLabel);
        });

        builder.Ignore(d => d.DomainEvents);
        builder.Ignore(d => d.PublishedVersion);
        builder.Ignore(d => d.InFlightVersion);
    }
}

public sealed class DocumentAcknowledgementConfiguration : IEntityTypeConfiguration<DocumentAcknowledgement>
{
    public void Configure(EntityTypeBuilder<DocumentAcknowledgement> builder)
    {
        builder.ToTable("document_acknowledgement", "qams");
        builder.HasKey(a => new { a.TenantId, a.Id });
        builder.Property(a => a.DocumentCode).HasMaxLength(60);
        builder.Property(a => a.VersionLabel).HasMaxLength(20);
        // One receipt per (document version, user): re-acknowledging is idempotent.
        // Pinned name (schema hardening 1.4): the EF default exceeded PostgreSQL's
        // 63-byte identifier limit and was silently truncated mid-word.
        builder.HasIndex(a => new { a.TenantId, a.DocumentId, a.VersionLabel, a.UserId })
            .IsUnique()
            .HasDatabaseName("ux_doc_ack_tenant_document_version_user");
        builder.HasIndex(a => new { a.TenantId, a.UserId });
        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class DocumentControlledCopyConfiguration : IEntityTypeConfiguration<DocumentControlledCopy>
{
    public void Configure(EntityTypeBuilder<DocumentControlledCopy> builder)
    {
        builder.ToTable("document_controlled_copy", "qams");
        builder.HasKey(c => new { c.TenantId, c.Id });
        builder.Property(c => c.DocumentCode).HasMaxLength(60);
        builder.Property(c => c.VersionLabel).HasMaxLength(20);
        builder.Property(c => c.Holder).HasMaxLength(200);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => new { c.TenantId, c.DocumentId, c.CopyNumber })
            .IsUnique()
            .HasDatabaseName("ux_doc_copy_tenant_document_number");
        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class FileReferenceConfiguration : IEntityTypeConfiguration<FileReference>
{
    public void Configure(EntityTypeBuilder<FileReference> builder)
    {
        builder.ToTable("file_reference", "qams");
        builder.HasKey(f => new { f.TenantId, f.Id });

        builder.Property(f => f.FileName).HasMaxLength(260);
        builder.Property(f => f.ContentType).HasMaxLength(150);
        builder.Property(f => f.Sha256).HasMaxLength(64);
        builder.Property(f => f.StorageKey).HasMaxLength(120);

        builder.HasIndex(f => new { f.TenantId, f.Sha256 });

        builder.Ignore(f => f.DomainEvents);
    }
}
