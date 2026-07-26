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
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Code).HasMaxLength(40);
        builder.Property(d => d.Title).HasMaxLength(300);
        builder.Property(d => d.Category).HasMaxLength(50);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(d => new { d.TenantId, d.Code }).IsUnique();
        builder.HasIndex(d => new { d.TenantId, d.Status });

        builder.OwnsMany(d => d.Versions, version =>
        {
            version.ToTable("document_version", "qams");
            version.WithOwner().HasForeignKey("document_id");
            version.HasKey(v => v.Id);
            version.Property(v => v.State).HasConversion<string>().HasMaxLength(20);
            version.Property(v => v.ChangeSummary).HasMaxLength(1000);
            version.Property(v => v.RejectionReason).HasMaxLength(1000);
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
        builder.HasKey(a => a.Id);
        builder.Property(a => a.DocumentCode).HasMaxLength(60);
        builder.Property(a => a.VersionLabel).HasMaxLength(20);
        // One receipt per (document version, user): re-acknowledging is idempotent.
        builder.HasIndex(a => new { a.TenantId, a.DocumentId, a.VersionLabel, a.UserId }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.UserId });
        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class FileReferenceConfiguration : IEntityTypeConfiguration<FileReference>
{
    public void Configure(EntityTypeBuilder<FileReference> builder)
    {
        builder.ToTable("file_reference", "qams");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName).HasMaxLength(260);
        builder.Property(f => f.ContentType).HasMaxLength(150);
        builder.Property(f => f.Sha256).HasMaxLength(64);
        builder.Property(f => f.StorageKey).HasMaxLength(120);

        builder.HasIndex(f => new { f.TenantId, f.Sha256 });

        builder.Ignore(f => f.DomainEvents);
    }
}
