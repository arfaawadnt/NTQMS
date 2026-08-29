namespace NT.QAMS.Contracts.Committees;

// ── Committee ────────────────────────────────────────────────────────────────

public sealed record CreateCommitteeRequest(string Name, string TermsOfReference, string Frequency, int QuorumSize);
public sealed record AddCommitteeMemberRequest(Guid UserId, string RoleTitle);
public sealed record UpdateQuorumRequest(int QuorumSize);

public sealed record CommitteeMemberDto(Guid Id, Guid UserId, string RoleTitle);

public sealed record CommitteeListItemDto(
    Guid Id, string Name, string Frequency, int QuorumSize, string Status, int MemberCount);

public sealed record CommitteeDetailDto(
    Guid Id, string Name, string TermsOfReference, string Frequency, int QuorumSize, string Status,
    IReadOnlyList<CommitteeMemberDto> Members);

// ── Meeting ──────────────────────────────────────────────────────────────────

public sealed record ScheduleMeetingRequest(Guid CommitteeId, DateTimeOffset ScheduledAtUtc);
public sealed record AddAgendaItemRequest(string Title, string? Detail, string? SourceRef, bool CarriedForward);
public sealed record RecordAttendanceRequest(Guid UserId, bool Present);
public sealed record AddDecisionRequest(string Description, Guid? OwnerId, DateOnly? DueDate);
public sealed record CloseDecisionRequest(string? Note);
public sealed record RecordMinutesRequest(string Minutes);
public sealed record ApproveMinutesRequest(string Password, string Pin);

public sealed record AgendaItemDto(Guid Id, string Title, string? Detail, string? SourceRef, bool CarriedForward);
public sealed record MeetingAttendanceDto(Guid Id, Guid UserId, bool Present);
public sealed record MeetingDecisionDto(
    Guid Id, string Description, Guid? OwnerId, DateOnly? DueDate, string Status, string? ClosureNote);

public sealed record MeetingListItemDto(
    Guid Id, Guid CommitteeId, string MeetingRef, DateTimeOffset ScheduledAtUtc, string Status,
    int PresentCount, int OpenDecisions);

public sealed record MeetingDetailDto(
    Guid Id, Guid CommitteeId, string MeetingRef, DateTimeOffset ScheduledAtUtc, string Status,
    string? Minutes, Guid? MinutesApprovedBy, int PresentCount,
    IReadOnlyList<AgendaItemDto> Agenda,
    IReadOnlyList<MeetingAttendanceDto> Attendance,
    IReadOnlyList<MeetingDecisionDto> Decisions);

/// <summary>An open action item, surfaced across all a committee's meetings for follow-through.</summary>
public sealed record OpenActionDto(
    Guid MeetingId, string MeetingRef, Guid DecisionId, string Description, Guid? OwnerId, DateOnly? DueDate);
