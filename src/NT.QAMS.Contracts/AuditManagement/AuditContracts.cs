namespace NT.QAMS.Contracts.AuditManagement;

public sealed record ScheduleAuditRequest(
    string Title, string Type, Guid LeadAuditorId, DateOnly PlannedDate,
    IReadOnlyList<ChecklistItemRequest> Checklist);

public sealed record ChecklistItemRequest(string IsoClause, string Question);

public sealed record AnswerChecklistItemRequest(string Verdict, string? Evidence);

public sealed record RaiseFindingRequest(string Grade, string Description);

public sealed record ChecklistItemDto(
    Guid Id, string IsoClause, string Question, string Verdict, string? Evidence);

public sealed record FindingDto(Guid Id, string Grade, string Description, Guid? NcId);

public sealed record AuditListItemDto(
    Guid Id, string AuditRef, string Title, string Type, string Status,
    Guid LeadAuditorId, DateOnly PlannedDate, DateTimeOffset CreatedAtUtc);

public sealed record AuditDetailDto(
    Guid Id, string AuditRef, string Title, string Type, string Status,
    Guid LeadAuditorId, DateOnly PlannedDate,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<FindingDto> Findings);
