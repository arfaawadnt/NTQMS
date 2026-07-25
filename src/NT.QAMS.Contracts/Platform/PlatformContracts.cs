namespace NT.QAMS.Contracts.Platform;

// ── Organization & reference data ────────────────────────────────────────────

public sealed record CreateBranchRequest(string Code, string Name, string? City);
public sealed record CreateDepartmentRequest(Guid BranchId, string Code, string Name);
public sealed record RenameRequest(string Name);
public sealed record CreateTestRequest(string TestCode, string TestName, string Methodology, int TurnaroundHours);
public sealed record UpsertLovRequest(
    string Category, string Code, string NameEn, string? NameAr, string? NameFr, int SortOrder);

public sealed record BranchDto(Guid Id, string Code, string Name, string? City, bool IsActive);
public sealed record DepartmentDto(Guid Id, Guid BranchId, string Code, string Name, bool IsActive);
public sealed record TestCatalogDto(
    Guid Id, string TestCode, string TestName, string Methodology, int TurnaroundHours, bool IsActive);
public sealed record LovDto(
    Guid Id, string Category, string Code, string NameEn, string? NameAr, string? NameFr,
    int SortOrder, bool IsActive);

// ── Notifications ────────────────────────────────────────────────────────────

public sealed record UpsertNotificationRuleRequest(
    string EventKey, string RecipientRoles, bool EmailEnabled,
    string SubjectTemplate, string BodyTemplate);

public sealed record NotificationRuleDto(
    Guid Id, string EventKey, string RecipientRoles, bool EmailEnabled,
    string SubjectTemplate, string BodyTemplate, bool IsActive);

public sealed record NotificationFeedItemDto(
    Guid Id, string EventKey, string Subject, string Body, bool Read,
    string EmailStatus, DateTimeOffset CreatedAtUtc);

public sealed record DispatchMonitorItemDto(
    Guid Id, string EventKey, Guid RecipientUserId, string? RecipientEmail,
    string Subject, string EmailStatus, string? Error, DateTimeOffset CreatedAtUtc);

// ── Context & Interested Parties (ISO 9001 §4.1/§4.2) ───────────────────────

public sealed record RegisterInterestedPartyRequest(
    string Name, string Category, string NeedsAndExpectations,
    string? RelevantRequirements, DateOnly ReviewedOn);

public sealed record ReviseInterestedPartyRequest(
    string Name, string Category, string NeedsAndExpectations,
    string? RelevantRequirements, DateOnly ReviewedOn);

public sealed record InterestedPartyDto(
    Guid Id, string PartyRef, string Name, string Category, string NeedsAndExpectations,
    string? RelevantRequirements, DateOnly ReviewedOn, string Status);

public sealed record RegisterContextIssueRequest(
    string Type, string Category, string Description, string Impact);

public sealed record ReviseContextIssueRequest(
    string Type, string Category, string Description, string Impact);

public sealed record LinkContextIssueRiskRequest(Guid RiskId);

public sealed record CloseContextIssueRequest(string Resolution);

public sealed record ContextIssueDto(
    Guid Id, string IssueRef, string Type, string Category, string Description, string Impact,
    Guid? LinkedRiskId, string Status, string? Resolution);
