using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Integration;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for the IntegrationEndpoint aggregate (HQMS M24). FORCE RLS in the migration.</summary>
public sealed class IntegrationEndpointConfiguration : IEntityTypeConfiguration<IntegrationEndpoint>
{
    public void Configure(EntityTypeBuilder<IntegrationEndpoint> builder)
    {
        builder.ToTable("integration_endpoint", "qams");
        builder.HasKey(e => new { e.TenantId, e.Id });

        builder.Property(e => e.Name).HasMaxLength(150);
        builder.Property(e => e.System).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Protocol).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.Status });
        builder.Ignore(e => e.IsHealthy);
        builder.Ignore(e => e.DomainEvents);
    }
}

/// <summary>
/// EF mapping for the IntegrationMessage inbox (HQMS M24). The (tenant, endpoint, dedup key)
/// unique index is what makes ingestion idempotent under redelivery.
/// </summary>
public sealed class IntegrationMessageConfiguration : IEntityTypeConfiguration<IntegrationMessage>
{
    public void Configure(EntityTypeBuilder<IntegrationMessage> builder)
    {
        builder.ToTable("integration_message", "qams");
        builder.HasKey(m => new { m.TenantId, m.Id });

        builder.Property(m => m.DedupKey).HasMaxLength(200);
        builder.Property(m => m.MessageType).HasMaxLength(40);
        builder.Property(m => m.RawPayload);
        builder.Property(m => m.ErrorDetail);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(m => new { m.TenantId, m.EndpointId, m.DedupKey })
            .IsUnique()
            .HasDatabaseName("ux_integration_message_dedup");
        builder.HasIndex(m => new { m.TenantId, m.EndpointId, m.Status });
        builder.Ignore(m => m.DomainEvents);
    }
}

/// <summary>EF mapping for the PatientStay ADT projection (HQMS M24). FORCE RLS in the migration.</summary>
public sealed class PatientStayConfiguration : IEntityTypeConfiguration<PatientStay>
{
    public void Configure(EntityTypeBuilder<PatientStay> builder)
    {
        builder.ToTable("patient_stay", "qams");
        builder.HasKey(s => new { s.TenantId, s.Id });

        builder.Property(s => s.PatientRef).HasMaxLength(100);
        builder.Property(s => s.EncounterRef).HasMaxLength(100);
        builder.Property(s => s.Unit).HasMaxLength(100);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => new { s.TenantId, s.EncounterRef })
            .IsUnique()
            .HasDatabaseName("ux_patient_stay_encounter");
        builder.HasIndex(s => new { s.TenantId, s.Status });
        builder.Ignore(s => s.DomainEvents);
    }
}
