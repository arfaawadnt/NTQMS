namespace NT.QAMS.Contracts.AuditManagement;

/// <summary>The two 21 CFR Part 11 identification components (§11.200(a)(1)) to sign off an audit.</summary>
public sealed record SignOffAuditRequest(string Password, string Pin);

public sealed record ScheduleAuditRequest(
    string Title, string Type, Guid LeadAuditorId, DateOnly PlannedDate,
    IReadOnlyList<ChecklistItemRequest> Checklist, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record ChecklistItemRequest(string IsoClause, string Question);

public sealed record AnswerChecklistItemRequest(string Verdict, string? Evidence);

public sealed record RaiseFindingRequest(string Grade, string Description);

public sealed record ChecklistItemDto(
    Guid Id, string IsoClause, string Question, string Verdict, string? Evidence);

public sealed record FindingDto(Guid Id, string Grade, string Description, Guid? NcId);

public sealed record AuditListItemDto(
    Guid Id, string AuditRef, string Title, string Type, string Status,
    Guid LeadAuditorId, DateOnly PlannedDate, DateTimeOffset CreatedAtUtc, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record AuditDetailDto(
    Guid Id, string AuditRef, string Title, string Type, string Status,
    Guid LeadAuditorId, DateOnly PlannedDate,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<FindingDto> Findings);
