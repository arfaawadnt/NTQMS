using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Tenancy;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenant", "saas");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Slug)
            .HasConversion(slug => slug.Value, value => TenantSlug.Create(value))
            .HasMaxLength(TenantSlug.MaxLength)
            .HasColumnName("identifier");

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.Name)
            .HasMaxLength(Tenant.MaxNameLength);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.SuspensionReason)
            .HasMaxLength(500);

        // 1:1 settings — owned, same row (settings split to its own table when
        // the column count warrants it, per the DB architecture).
        builder.OwnsOne(t => t.Settings, settings =>
        {
            settings.Property(s => s.PasswordExpiryDays).HasColumnName("password_expiry_days");
            settings.Property(s => s.CalibrationReminderDays).HasColumnName("calibration_reminder_days");
            settings.Property(s => s.SopExpiryReminderMonths).HasColumnName("sop_expiry_reminder_months");
            settings.Property(s => s.DefaultLanguage).HasColumnName("default_language").HasMaxLength(5);
            settings.Property(s => s.TimeZone).HasColumnName("time_zone").HasMaxLength(60);
            settings.Property(s => s.RequireMfaForPrivilegedRoles).HasColumnName("require_mfa_privileged");
        });

        builder.Ignore(t => t.DomainEvents);
    }
}
