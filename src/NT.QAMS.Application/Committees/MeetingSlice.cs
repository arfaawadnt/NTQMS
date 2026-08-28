using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Committees;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Committees;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Committees;

// ── Commands ─────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Create)]
public sealed record ScheduleMeetingCommand(Guid CommitteeId, DateTimeOffset ScheduledAtUtc) : ICommand<Guid>;

public sealed class ScheduleMeetingHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ScheduleMeetingCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleMeetingCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        var committee = await db.Committees.AsNoTracking()
            .Where(x => x.Id == c.CommitteeId).Select(x => new { x.Status }).SingleOrDefaultAsync(ct)
            ?? throw new DomainException("CMT-404", "Committee not found.");
        if (committee.Status != CommitteeStatus.Active)
        {
            // M-16: a disbanded committee is governance history — it conducts nothing.
            throw new DomainException("CMT-016", "A disbanded committee cannot conduct meetings.");
        }

        var meetingRef = await refs.NextAsync(tenantId, "MTG", ct);
        var meeting = Meeting.Schedule(c.CommitteeId, meetingRef, c.ScheduledAtUtc);
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync(ct);
        return meeting.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record AddAgendaItemCommand(
    Guid MeetingId, string Title, string? Detail, string? SourceRef, bool CarriedForward) : ICommand<Guid>;

public sealed class AddAgendaItemValidator : AbstractValidator<AddAgendaItemCommand>
{
    public AddAgendaItemValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Detail).MaximumLength(2000);
        RuleFor(x => x.SourceRef).MaximumLength(120);
    }
}

public sealed class AddAgendaItemHandler(IAppDbContext db) : ICommandHandler<AddAgendaItemCommand, Guid>
{
    public async Task<Guid> Handle(AddAgendaItemCommand c, CancellationToken ct)
    {
        var meeting = await Load(db, c.MeetingId, ct);
        var id = meeting.AddAgendaItem(c.Title, c.Detail, c.SourceRef, c.CarriedForward);
        await db.SaveChangesAsync(ct);
        return id;
    }

    internal static async Task<Meeting> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Meetings
            .Include(m => m.Agenda).Include(m => m.Attendance).Include(m => m.Decisions)
            .SingleOrDefaultAsync(m => m.Id == id, ct)
        ?? throw new DomainException("MTG-404", "Meeting not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record RecordAttendanceCommand(Guid MeetingId, Guid UserId, bool Present) : ICommand;

public sealed class RecordAttendanceHandler(IAppDbContext db) : ICommandHandler<RecordAttendanceCommand>
{
    public async Task Handle(RecordAttendanceCommand c, CancellationToken ct)
    {
        var meeting = await AddAgendaItemHandler.Load(db, c.MeetingId, ct);

        // M-16: quorum counts attendance rows — they must belong to actual
        // committee members, not arbitrary Guids.
        var isMember = await db.Committees.AsNoTracking()
            .Where(x => x.Id == meeting.CommitteeId)
            .AnyAsync(x => x.Members.Any(m => m.UserId == c.UserId), ct);
        if (!isMember)
        {
            throw new DomainException("CMT-017", "Only a committee member can be marked as an attendee.");
        }

        meeting.RecordAttendance(c.UserId, c.Present);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Approve)]
public sealed record HoldMeetingCommand(Guid MeetingId) : ICommand;

public sealed class HoldMeetingHandler(IAppDbContext db) : ICommandHandler<HoldMeetingCommand>
{
    public async Task Handle(HoldMeetingCommand c, CancellationToken ct)
    {
        var meeting = await AddAgendaItemHandler.Load(db, c.MeetingId, ct);
        var committee = await db.Committees.AsNoTracking()
            .Where(x => x.Id == meeting.CommitteeId)
            .Select(x => new { x.QuorumSize, x.Status }).SingleOrDefaultAsync(ct)
            ?? throw new DomainException("CMT-404", "Committee not found.");
        if (committee.Status != CommitteeStatus.Active)
        {
            // M-16: disbanding between scheduling and the meeting kills the meeting.
            throw new DomainException("CMT-016", "A disbanded committee cannot conduct meetings.");
        }

        meeting.Hold(committee.QuorumSize);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record AddDecisionCommand(Guid MeetingId, string Description, Guid? OwnerId, DateOnly? DueDate) : ICommand<Guid>;

public sealed class AddDecisionValidator : AbstractValidator<AddDecisionCommand>
{
    public AddDecisionValidator() => RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
}

public sealed class AddDecisionHandler(IAppDbContext db) : ICommandHandler<AddDecisionCommand, Guid>
{
    public async Task<Guid> Handle(AddDecisionCommand c, CancellationToken ct)
    {
        var meeting = await AddAgendaItemHandler.Load(db, c.MeetingId, ct);
        var id = meeting.AddDecision(c.Description, c.OwnerId, c.DueDate);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record CloseDecisionCommand(Guid MeetingId, Guid DecisionId, string? Note) : ICommand;

// The closure note's bound lives here — the column is text (hardening 1.2, N-05).
public sealed class CloseDecisionValidator : AbstractValidator<CloseDecisionCommand>
{
    public CloseDecisionValidator() => RuleFor(x => x.Note).MaximumLength(2000);
}

public sealed class CloseDecisionHandler(IAppDbContext db) : ICommandHandler<CloseDecisionCommand>
{
    public async Task Handle(CloseDecisionCommand c, CancellationToken ct)
    {
        var meeting = await AddAgendaItemHandler.Load(db, c.MeetingId, ct);
        meeting.CloseDecision(c.DecisionId, c.Note);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record RecordMinutesCommand(Guid MeetingId, string Minutes) : ICommand;

public sealed class RecordMinutesValidator : AbstractValidator<RecordMinutesCommand>
{
    public RecordMinutesValidator() => RuleFor(x => x.Minutes).NotEmpty().MaximumLength(20000);
}

public sealed class RecordMinutesHandler(IAppDbContext db) : ICommandHandler<RecordMinutesCommand>
{
    public async Task Handle(RecordMinutesCommand c, CancellationToken ct)
    {
        var meeting = await AddAgendaItemHandler.Load(db, c.MeetingId, ct);
        meeting.RecordMinutes(c.Minutes);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Approve)]
public sealed record ApproveMinutesCommand(Guid MeetingId) : ICommand;

public sealed class ApproveMinutesHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<ApproveMinutesCommand>
{
    public async Task Handle(ApproveMinutesCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var meeting = await AddAgendaItemHandler.Load(db, c.MeetingId, ct);

        // M-16: the approval mints governance evidence — not for a committee
        // that no longer exists.
        var active = await db.Committees.AsNoTracking()
            .AnyAsync(x => x.Id == meeting.CommitteeId && x.Status == CommitteeStatus.Active, ct);
        if (!active)
        {
            throw new DomainException("CMT-016", "A disbanded committee cannot conduct meetings.");
        }

        meeting.ApproveMinutes(actor);
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetMeetingsQuery(Guid CommitteeId) : IQuery<IReadOnlyList<MeetingListItemDto>>;

public sealed class GetMeetingsHandler(IAppDbContext db) : IQueryHandler<GetMeetingsQuery, IReadOnlyList<MeetingListItemDto>>
{
    public async Task<IReadOnlyList<MeetingListItemDto>> Handle(GetMeetingsQuery q, CancellationToken ct)
    {
        var meetings = await db.Meetings.AsNoTracking()
            .Include(m => m.Attendance).Include(m => m.Decisions)
            .Where(m => m.CommitteeId == q.CommitteeId)
            .OrderByDescending(m => m.ScheduledAtUtc)
            .ToListAsync(ct);

        return meetings
            .Select(m => new MeetingListItemDto(
                m.Id, m.CommitteeId, m.MeetingRef, m.ScheduledAtUtc, m.Status.ToString(),
                m.Attendance.Count(a => a.Present), m.Decisions.Count(d => d.Status == DecisionStatus.Open)))
            .ToList();
    }
}

public sealed record GetMeetingByIdQuery(Guid MeetingId) : IQuery<MeetingDetailDto>;

public sealed class GetMeetingByIdHandler(IAppDbContext db) : IQueryHandler<GetMeetingByIdQuery, MeetingDetailDto>
{
    public async Task<MeetingDetailDto> Handle(GetMeetingByIdQuery q, CancellationToken ct)
    {
        var m = await db.Meetings.AsNoTracking()
            .Include(x => x.Agenda).Include(x => x.Attendance).Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == q.MeetingId, ct)
            ?? throw new DomainException("MTG-404", "Meeting not found.");

        return new MeetingDetailDto(
            m.Id, m.CommitteeId, m.MeetingRef, m.ScheduledAtUtc, m.Status.ToString(),
            m.Minutes, m.MinutesApprovedBy, m.Attendance.Count(a => a.Present),
            m.Agenda.Select(a => new AgendaItemDto(a.Id, a.Title, a.Detail, a.SourceRef, a.CarriedForward)).ToList(),
            m.Attendance.Select(a => new MeetingAttendanceDto(a.Id, a.UserId, a.Present)).ToList(),
            m.Decisions.Select(d => new MeetingDecisionDto(
                d.Id, d.Description, d.OwnerId, d.DueDate, d.Status.ToString(), d.ClosureNote)).ToList());
    }
}

/// <summary>
/// Every open action item across a committee's meetings — the cross-meeting follow-through
/// register that is the point of the module.
/// </summary>
public sealed record GetOpenActionsQuery(Guid CommitteeId) : IQuery<IReadOnlyList<OpenActionDto>>;

public sealed class GetOpenActionsHandler(IAppDbContext db) : IQueryHandler<GetOpenActionsQuery, IReadOnlyList<OpenActionDto>>
{
    public async Task<IReadOnlyList<OpenActionDto>> Handle(GetOpenActionsQuery q, CancellationToken ct)
    {
        var meetings = await db.Meetings.AsNoTracking()
            .Include(m => m.Decisions)
            .Where(m => m.CommitteeId == q.CommitteeId)
            .ToListAsync(ct);

        return meetings
            .SelectMany(m => m.Decisions
                .Where(d => d.Status == DecisionStatus.Open)
                .Select(d => new OpenActionDto(m.Id, m.MeetingRef, d.Id, d.Description, d.OwnerId, d.DueDate)))
            .OrderBy(a => a.DueDate ?? DateOnly.MaxValue)
            .ToList();
    }
}
