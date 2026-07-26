namespace NT.QAMS.Contracts.Operations;

// ── Records & retention ──────────────────────────────────────────────────────

public sealed record ArchiveRecordRequest(
    string SourceModule, string SourceRef, Guid SnapshotFileId, string RetentionClass);

public sealed record PlaceLegalHoldRequest(string Reason);

public sealed record ArchiveListItemDto(
    Guid Id, string ArchiveRef, string SourceModule, string SourceRef,
    string RetentionClass, DateOnly ArchivedOn, DateOnly? RetentionExpiry, string State, bool IsOnLegalHold);

// ── SLA definitions ──────────────────────────────────────────────────────────

public sealed record UpsertSlaRequest(string Module, string Severity, int TargetHours);
public sealed record SlaDefinitionDto(Guid Id, string Module, string Severity, int TargetHours);

// ── Work tasks ───────────────────────────────────────────────────────────────

public sealed record CreateTaskRequest(
    string Subject, string? SubjectRef, Guid? AssigneeUserId, string? AssigneeRole, DateOnly DueDate);

public sealed record WorkTaskDto(
    Guid Id, string Subject, string? SubjectRef, Guid? AssigneeUserId, string? AssigneeRole,
    DateOnly DueDate, string Status, bool Overdue);
