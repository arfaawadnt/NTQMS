using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Committees;

/// <summary>Lifecycle of a meeting.</summary>
public enum MeetingStatus { Scheduled, Held, MinutesApproved, Cancelled }

/// <summary>Whether a committee decision/action is still open or has been closed out.</summary>
public enum DecisionStatus { Open, Closed }

/// <summary>An agenda line for a meeting; may be carried forward or pulled from another module.</summary>
public sealed class AgendaItem : Entity
{
    internal AgendaItem(string title, string? detail, string? sourceRef, bool carriedForward)
    {
        Title = title;
        Detail = detail;
        SourceRef = sourceRef;
        CarriedForward = carriedForward;
    }

    private AgendaItem() { Title = null!; }

    public string Title { get; private set; }
    public string? Detail { get; private set; }

    /// <summary>Origin reference when the item was pulled from another record (e.g. "CAPA:NC-2026-0007", "KPI:HH-1").</summary>
    public string? SourceRef { get; private set; }

    /// <summary>True when carried forward from a previous meeting's open items.</summary>
    public bool CarriedForward { get; private set; }
}

/// <summary>Attendance record for one invited/present member.</summary>
public sealed class MeetingAttendance : Entity
{
    internal MeetingAttendance(Guid userId, bool present)
    {
        UserId = userId;
        Present = present;
    }

    private MeetingAttendance() { }

    public Guid UserId { get; private set; }
    public bool Present { get; internal set; }
}

/// <summary>
/// A decision or action item recorded in a meeting. Action items carry an owner and due
/// date and are tracked across meetings until closed — the single most valuable function
/// of the governance module.
/// </summary>
public sealed class MeetingDecision : Entity
{
    internal MeetingDecision(string description, Guid? ownerId, DateOnly? dueDate)
    {
        Description = description;
        OwnerId = ownerId;
        DueDate = dueDate;
        Status = DecisionStatus.Open;
    }

    private MeetingDecision() { Description = null!; }

    public string Description { get; private set; }
    public Guid? OwnerId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DecisionStatus Status { get; private set; }
    public string? ClosureNote { get; private set; }

    internal void Close(string? note)
    {
        Status = DecisionStatus.Closed;
        ClosureNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>
/// A committee meeting (HQMS M17): scheduled, then held once quorum is met, with an agenda,
/// attendance, decisions/action items and minutes that are approved at the following meeting.
/// A meeting references its committee by id (a separate aggregate).
/// </summary>
public sealed class Meeting : AggregateRoot, ITenantScoped
{
    private readonly List<AgendaItem> _agenda = [];
    private readonly List<MeetingAttendance> _attendance = [];
    private readonly List<MeetingDecision> _decisions = [];

    private Meeting() { MeetingRef = null!; }

    public Guid TenantId { get; set; }
    public Guid CommitteeId { get; private set; }
    public string MeetingRef { get; private set; }
    public DateTimeOffset ScheduledAtUtc { get; private set; }
    public MeetingStatus Status { get; private set; }
    public string? Minutes { get; private set; }
    public Guid? MinutesApprovedBy { get; private set; }

    public IReadOnlyList<AgendaItem> Agenda => _agenda.AsReadOnly();
    public IReadOnlyList<MeetingAttendance> Attendance => _attendance.AsReadOnly();
    public IReadOnlyList<MeetingDecision> Decisions => _decisions.AsReadOnly();

    /// <summary>Members recorded present.</summary>
    public int PresentCount => _attendance.Count(a => a.Present);

    public static Meeting Schedule(Guid committeeId, string meetingRef, DateTimeOffset scheduledAtUtc)
    {
        if (committeeId == Guid.Empty)
        {
            throw new DomainException("MTG-001", "A committee is required.");
        }

        return new Meeting
        {
            CommitteeId = committeeId,
            MeetingRef = meetingRef,
            ScheduledAtUtc = scheduledAtUtc,
            Status = MeetingStatus.Scheduled,
        };
    }

    public Guid AddAgendaItem(string title, string? detail, string? sourceRef, bool carriedForward)
    {
        RequireStatus(MeetingStatus.Scheduled, "MTG-010", "add an agenda item to");
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("MTG-011", "An agenda item title is required.");
        }

        var item = new AgendaItem(
            title.Trim(), string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
            string.IsNullOrWhiteSpace(sourceRef) ? null : sourceRef.Trim(), carriedForward);
        _agenda.Add(item);
        return item.Id;
    }

    public void RecordAttendance(Guid userId, bool present)
    {
        RequireStatus(MeetingStatus.Scheduled, "MTG-012", "record attendance for");
        var existing = _attendance.FirstOrDefault(a => a.UserId == userId);
        if (existing is not null)
        {
            existing.Present = present;
            return;
        }

        _attendance.Add(new MeetingAttendance(userId, present));
    }

    /// <summary>
    /// Holds the meeting once quorum is met (Scheduled ⇒ Held). The committee's quorum is
    /// supplied by the caller since the committee is a separate aggregate.
    /// </summary>
    public void Hold(int committeeQuorum)
    {
        RequireStatus(MeetingStatus.Scheduled, "MTG-013", "hold");
        if (PresentCount < committeeQuorum)
        {
            throw new DomainException(
                "MTG-014", $"The meeting is not quorate: {PresentCount} present, {committeeQuorum} required.");
        }

        Status = MeetingStatus.Held;
        Raise(new MeetingHeld(Id, CommitteeId, MeetingRef, PresentCount));
    }

    public Guid AddDecision(string description, Guid? ownerId, DateOnly? dueDate)
    {
        RequireStatus(MeetingStatus.Held, "MTG-015", "add a decision to");
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("MTG-016", "A decision description is required.");
        }

        var decision = new MeetingDecision(description.Trim(), ownerId, dueDate);
        _decisions.Add(decision);
        return decision.Id;
    }

    public void CloseDecision(Guid decisionId, string? note)
    {
        var decision = _decisions.FirstOrDefault(d => d.Id == decisionId)
            ?? throw new DomainException("MTG-017", "Decision not found in this meeting.");
        if (decision.Status == DecisionStatus.Closed)
        {
            throw new InvalidStateTransitionException("MTG-018", "The decision is already closed.");
        }

        decision.Close(note);
    }

    public void RecordMinutes(string minutes)
    {
        RequireStatus(MeetingStatus.Held, "MTG-019", "record minutes for");
        if (string.IsNullOrWhiteSpace(minutes))
        {
            throw new DomainException("MTG-020", "Minutes text is required.");
        }

        Minutes = minutes.Trim();
    }

    /// <summary>
    /// Approves the minutes (Held ⇒ MinutesApproved), the governance evidence that the meeting
    /// happened and its record is agreed. Minutes must exist first.
    /// </summary>
    public void ApproveMinutes(Guid approverId)
    {
        RequireStatus(MeetingStatus.Held, "MTG-021", "approve minutes for");
        if (string.IsNullOrWhiteSpace(Minutes))
        {
            throw new DomainException("MTG-022", "Minutes must be recorded before approval.");
        }

        Status = MeetingStatus.MinutesApproved;
        MinutesApprovedBy = approverId;
        Raise(new MeetingMinutesApproved(Id, CommitteeId, MeetingRef, approverId));
    }

    public void Cancel()
    {
        if (Status is MeetingStatus.Held or MeetingStatus.MinutesApproved)
        {
            throw new InvalidStateTransitionException("MTG-023", "A held meeting cannot be cancelled.");
        }

        Status = MeetingStatus.Cancelled;
    }

    private void RequireStatus(MeetingStatus expected, string code, string action)
    {
        if (Status != expected)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} a meeting in state {Status}.");
        }
    }
}

public sealed record MeetingHeld(Guid MeetingId, Guid CommitteeId, string MeetingRef, int PresentCount) : DomainEvent;
public sealed record MeetingMinutesApproved(Guid MeetingId, Guid CommitteeId, string MeetingRef, Guid ApprovedBy) : DomainEvent;
