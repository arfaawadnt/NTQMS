using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Infrastructure.Persistence.Idempotency;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_record", "qams");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.IdempotencyKey).HasMaxLength(100);
        builder.Property(r => r.RequestType).HasMaxLength(300);

        // The replay anchor: one stored response per (actor, key, command type).
        builder.HasIndex(r => new { r.ActorId, r.IdempotencyKey, r.RequestType })
            .IsUnique()
            .HasDatabaseName("ux_idempotency_actor_key");

        // The retention purge's scan path.
        builder.HasIndex(r => r.CreatedAtUtc)
            .HasDatabaseName("ix_idempotency_created");
    }
}
