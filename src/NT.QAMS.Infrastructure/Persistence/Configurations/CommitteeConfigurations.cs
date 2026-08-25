using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.Committees;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the Committee aggregate (HQMS M17). Tenant-first composite key, enum
/// CHECK domains in the migration, and an owned member child with a shadow tenant column
/// and composite ownership FK. FORCE RLS is added in the migration.
/// </summary>
public sealed class CommitteeConfiguration : IEntityTypeConfiguration<Committee>
{
    public void Configure(EntityTypeBuilder<Committee> builder)
    {
        builder.ToTable("committee", "qams");
        builder.HasKey(c => new { c.TenantId, c.Id });

        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.TermsOfReference);
        builder.Property(c => c.Frequency).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.Status });

        builder.OwnsMany(c => c.Members, m =>
        {
            m.ToTable("committee_member", "qams");
            m.Property<Guid>("TenantId");
            m.WithOwner().HasForeignKey("TenantId", "committee_id");
            m.HasKey("TenantId", "Id");
            m.Property(x => x.RoleTitle).HasMaxLength(100);
        });

        builder.Ignore(c => c.DomainEvents);
    }
}

/// <summary>
/// EF mapping for the Meeting aggregate (HQMS M17). Owns agenda items, attendance and
/// decisions, each with a shadow tenant column and composite ownership FK. FORCE RLS is
/// added in the migration.
/// </summary>
public sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("meeting", "qams");
        builder.HasKey(m => new { m.TenantId, m.Id });

        builder.Property(m => m.MeetingRef).HasMaxLength(30);
        builder.Property(m => m.Minutes);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(m => new { m.TenantId, m.CommitteeId });
        builder.HasIndex(m => new { m.TenantId, m.MeetingRef }).IsUnique();

        builder.Ignore(m => m.PresentCount);

        builder.OwnsMany(m => m.Agenda, a =>
        {
            a.ToTable("meeting_agenda_item", "qams");
            a.Property<Guid>("TenantId");
            a.WithOwner().HasForeignKey("TenantId", "meeting_id");
            a.HasKey("TenantId", "Id");
            a.Property(x => x.Title).HasMaxLength(300);
            a.Property(x => x.Detail).HasMaxLength(2000);
            a.Property(x => x.SourceRef).HasMaxLength(120);
        });

        builder.OwnsMany(m => m.Attendance, at =>
        {
            at.ToTable("meeting_attendance", "qams");
            at.Property<Guid>("TenantId");
            at.WithOwner().HasForeignKey("TenantId", "meeting_id");
            at.HasKey("TenantId", "Id");
        });

        builder.OwnsMany(m => m.Decisions, d =>
        {
            d.ToTable("meeting_decision", "qams");
            d.Property<Guid>("TenantId");
            d.WithOwner().HasForeignKey("TenantId", "meeting_id");
            d.HasKey("TenantId", "Id");
            d.Property(x => x.Description).HasMaxLength(2000);
            d.Property(x => x.ClosureNote).HasMaxLength(2000);
            d.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        builder.Ignore(m => m.DomainEvents);
    }
}
