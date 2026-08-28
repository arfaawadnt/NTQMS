using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.TrainingManagement;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the TrainingCourse catalogue aggregate (HQMS M12). Tenant-first composite key,
/// enum-as-string columns fenced by CHECK domains in the migration. FORCE RLS in the migration.
/// </summary>
public sealed class TrainingCourseConfiguration : IEntityTypeConfiguration<TrainingCourse>
{
    public void Configure(EntityTypeBuilder<TrainingCourse> builder)
    {
        builder.ToTable("training_course", "qams");
        builder.HasKey(c => new { c.TenantId, c.Id });

        builder.Property(c => c.CourseRef).HasMaxLength(30);
        builder.Property(c => c.Title).HasMaxLength(200);
        builder.Property(c => c.Description);
        builder.Property(c => c.DurationHours).HasPrecision(6, 2);
        builder.Property(c => c.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.CourseRef }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Category, c.Status });

        builder.Ignore(c => c.DomainEvents);
    }
}

/// <summary>
/// EF mapping for the TrainingSession aggregate (HQMS M12) with its owned attendance lines. The
/// child carries a shadow tenant_id and a composite FK to the session. FORCE RLS in the migration.
/// </summary>
public sealed class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("training_session", "qams");
        builder.HasKey(s => new { s.TenantId, s.Id });

        builder.Property(s => s.SessionRef).HasMaxLength(30);
        builder.Property(s => s.Location).HasMaxLength(200);
        builder.Property(s => s.TrainerName).HasMaxLength(200);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => new { s.TenantId, s.SessionRef }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.CourseId });
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.Ignore(s => s.AttendedCount);

        builder.OwnsMany(s => s.Attendance, a =>
        {
            a.ToTable("training_session_attendance", "qams");
            a.Property<Guid>("TenantId");
            a.WithOwner().HasForeignKey("TenantId", "training_session_id")
                .HasConstraintName("fk_ts_attendance_training_session_tenant_id_session_id");
            a.HasKey("TenantId", "Id");
            a.Ignore(x => x.ScoreGain);
        });

        builder.Ignore(s => s.DomainEvents);
    }
}
