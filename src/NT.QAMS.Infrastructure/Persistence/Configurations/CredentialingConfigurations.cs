using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Credentialing;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the Practitioner aggregate (HQMS M13) with its owned licence and privilege
/// collections. Each child carries a shadow tenant_id and a composite FK to the practitioner.
/// FORCE RLS on all three tables in the migration.
/// </summary>
public sealed class PractitionerConfiguration : IEntityTypeConfiguration<Practitioner>
{
    public void Configure(EntityTypeBuilder<Practitioner> builder)
    {
        builder.ToTable("practitioner", "qams");
        builder.HasKey(p => new { p.TenantId, p.Id });

        builder.Property(p => p.PractitionerRef).HasMaxLength(30);
        builder.Property(p => p.FullName).HasMaxLength(200);
        builder.Property(p => p.Specialty).HasMaxLength(150);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(p => new { p.TenantId, p.PractitionerRef }).IsUnique()
            .HasDatabaseName("ux_practitioner_tenant_id_practitioner_ref");
        builder.HasIndex(p => new { p.TenantId, p.Status });
        builder.HasIndex(p => new { p.TenantId, p.Specialty });

        builder.OwnsMany(p => p.Licences, l =>
        {
            l.ToTable("practitioner_licence", "qams");
            l.Property<Guid>("TenantId");
            l.WithOwner().HasForeignKey("TenantId", "practitioner_id");
            l.HasKey("TenantId", "Id");
            l.Property(x => x.Identifier).HasMaxLength(100);
            l.Property(x => x.Issuer).HasMaxLength(150);
            l.Property(x => x.VerificationSource).HasMaxLength(300);
            l.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            l.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(20);
        });

        builder.OwnsMany(p => p.Privileges, pr =>
        {
            pr.ToTable("practitioner_privilege", "qams");
            pr.Property<Guid>("TenantId");
            pr.WithOwner().HasForeignKey("TenantId", "practitioner_id")
                .HasConstraintName("fk_prac_priv_practitioner_tenant_id_practitioner_id");
            pr.HasKey("TenantId", "Id");
            pr.Property(x => x.Name).HasMaxLength(200);
            pr.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.Ignore(p => p.DomainEvents);
    }
}
