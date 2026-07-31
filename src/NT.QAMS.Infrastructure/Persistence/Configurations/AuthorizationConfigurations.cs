using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Authorization;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("role", "qams");
        builder.HasKey(r => new { r.TenantId, r.Id });

        builder.Property(r => r.Name).HasMaxLength(80);
        builder.Property(r => r.NormalizedName).HasMaxLength(80);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.DefaultLanguage).HasMaxLength(10);

        // Case-insensitive uniqueness per tenant, via the normalized column.
        builder.HasIndex(r => new { r.TenantId, r.NormalizedName }).IsUnique();

        builder.OwnsMany(r => r.Permissions, permission =>
        {
            permission.ToTable("role_permission", "qams");
            // Shadow tenant column (schema hardening Phase 4): stamped from the
            // owner by TenantStampInterceptor; the composite FK to the owner makes
            // a mismatched value impossible to persist. RLS reads it.
            permission.Property<Guid>("TenantId");
            permission.WithOwner().HasForeignKey("TenantId", "role_id");
            permission.Property(p => p.PermissionKey).HasMaxLength(60);
            permission.HasKey("TenantId", "role_id", nameof(RolePermission.PermissionKey));
        });

        builder.Ignore(r => r.PermissionKeys);
        builder.Ignore(r => r.DomainEvents);
    }
}
