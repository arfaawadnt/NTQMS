namespace NT.QAMS.Contracts.Improvement;

public sealed record RaiseNcRequest(
    string Title, string Description, int Severity, int Likelihood, string SourceType,
    Guid? BranchId = null, Guid? DepartmentId = null, string EventType = "Nonconformity");

public sealed record TriageNcRequest(Guid AssigneeId);

public sealed record RejectNcRequest(string Reason);

public sealed record RecordRcaRequest(string Method, string Analysis);

public sealed record PlanCapaActionRequest(string Type, string Details, Guid OwnerId, DateOnly DueDate);

public sealed record VerifyNcRequest(bool Passed, string Password, string Pin);

public sealed record ConfirmEffectivenessRequest(bool Effective, string Password, string Pin);

public sealed record ReopenNcRequest(string Reason, string Password, string Pin);

public sealed record CapaActionDto(
    Guid Id, string Type, string Details, Guid OwnerId, DateOnly DueDate,
    string Status, DateTimeOffset? CompletedAtUtc);

public sealed record RcaRecordDto(Guid Id, string Method, string Analysis, Guid InvestigatorId);

public sealed record NcListItemDto(
    Guid Id, string NcRef, string Title, string Status, int Severity, int Rpn,
    string SourceType, DateTimeOffset CreatedAtUtc, string EventType = "Nonconformity",
    Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record NcDetailDto(
    Guid Id, string NcRef, string Title, string Description, string Status,
    int Severity, int Likelihood, int Rpn, string SourceType, string EventType,
    Guid RaisedBy, Guid? AssignedTo, string? RejectionReason,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<CapaActionDto> CapaActions,
    IReadOnlyList<RcaRecordDto> RcaRecords);
