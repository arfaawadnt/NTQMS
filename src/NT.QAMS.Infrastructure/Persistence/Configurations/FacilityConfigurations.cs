using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Facility;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class MonitoringPointConfiguration : IEntityTypeConfiguration<MonitoringPoint>
{
    public void Configure(EntityTypeBuilder<MonitoringPoint> builder)
    {
        builder.ToTable("monitoring_point", "qams");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PointRef).HasMaxLength(30);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Location).HasMaxLength(200);
        builder.Property(p => p.Parameter).HasMaxLength(100);
        builder.Property(p => p.Unit).HasMaxLength(30);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(p => new { p.TenantId, p.PointRef }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Status });

        builder.OwnsMany(p => p.Readings, reading =>
        {
            reading.ToTable("environmental_reading", "qams");
            reading.WithOwner().HasForeignKey("point_id");
            reading.HasKey(r => r.Id);
            reading.Property(r => r.Remark).HasMaxLength(1000);
            reading.HasIndex("point_id", nameof(EnvironmentalReading.RecordedAtUtc));
        });

        builder.Ignore(p => p.DomainEvents);
    }
}
