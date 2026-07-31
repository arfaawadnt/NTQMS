using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Records;
using NT.QAMS.Domain.Sla;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class ArchiveEntryConfiguration : IEntityTypeConfiguration<ArchiveEntry>
{
    public void Configure(EntityTypeBuilder<ArchiveEntry> builder)
    {
        builder.ToTable("archive_entry", "qams");
        builder.HasKey(a => new { a.TenantId, a.Id });
        builder.Property(a => a.ArchiveRef).HasMaxLength(30);
        builder.Property(a => a.SourceModule).HasMaxLength(50);
        builder.Property(a => a.SourceRef).HasMaxLength(60);
        builder.Property(a => a.RetentionClass).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.State).HasConversion<string>().HasMaxLength(15);
        builder.HasIndex(a => new { a.TenantId, a.SourceModule, a.SourceRef }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.State });
        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class SlaDefinitionConfiguration : IEntityTypeConfiguration<SlaDefinition>
{
    public void Configure(EntityTypeBuilder<SlaDefinition> builder)
    {
        builder.ToTable("sla_definition", "qams");
        builder.HasKey(s => new { s.TenantId, s.Id });
        builder.Property(s => s.Module).HasMaxLength(50);
        builder.Property(s => s.Severity).HasMaxLength(30);
        builder.HasIndex(s => new { s.TenantId, s.Module, s.Severity }).IsUnique();
        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("work_task", "qams");
        builder.HasKey(t => new { t.TenantId, t.Id });
        builder.Property(t => t.Subject).HasMaxLength(300);
        builder.Property(t => t.SubjectRef).HasMaxLength(80);
        builder.Property(t => t.AssigneeRole).HasMaxLength(30);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(15);
        builder.HasIndex(t => new { t.TenantId, t.AssigneeUserId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.AssigneeRole, t.Status });
        builder.HasIndex(t => t.SubjectRef);
        builder.Ignore(t => t.DomainEvents);
    }
}

public sealed class EscalationTimerConfiguration : IEntityTypeConfiguration<EscalationTimer>
{
    public void Configure(EntityTypeBuilder<EscalationTimer> builder)
    {
        builder.ToTable("escalation_timer", "qams");
        builder.HasKey(t => new { t.TenantId, t.Id });
        builder.Property(t => t.SubjectRef).HasMaxLength(80);
        builder.HasIndex(t => t.SubjectRef);
        // The tick's only read path: the active frontier.
        builder.HasIndex(t => t.NextStepAtUtc).HasFilter("active = true");
        builder.Ignore(t => t.DomainEvents);
    }
}
