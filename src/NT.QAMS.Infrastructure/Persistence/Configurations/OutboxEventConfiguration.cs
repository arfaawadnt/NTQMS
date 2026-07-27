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

        // The processor's only read path: the live (not processed, not
        // dead-lettered) frontier, claimed oldest-first.
        builder.HasIndex(e => e.OccurredAtUtc)
            .HasFilter("processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL")
            .HasDatabaseName("ix_outbox_event_pending");

        // Dead-letter triage path: operators list poison events oldest-first.
        builder.HasIndex(e => e.DeadLetteredAtUtc)
            .HasFilter("dead_lettered_at_utc IS NOT NULL")
            .HasDatabaseName("ix_outbox_event_dead_letter");
    }
}
