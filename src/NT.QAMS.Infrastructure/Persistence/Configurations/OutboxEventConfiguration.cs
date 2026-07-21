using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Infrastructure.Persistence.Outbox;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_event", "qams");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasMaxLength(400);
        builder.Property(e => e.LastError).HasMaxLength(2000);

        // The processor's only read path: the unprocessed frontier.
        builder.HasIndex(e => e.OccurredAtUtc)
            .HasFilter("processed_at_utc IS NULL")
            .HasDatabaseName("ix_outbox_event_pending");
    }
}
