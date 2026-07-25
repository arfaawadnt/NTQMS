namespace NT.QAMS.Contracts.Improvement;

// ── Quality Objectives & Targets (ISO 9001 §6.2 / ISO 17025 §8.2) ───────────

public sealed record DefineQualityObjectiveRequest(
    string Title, string? Description, string Metric, string Unit,
    decimal TargetValue, string Direction, Guid OwnerId,
    DateOnly PeriodStart, DateOnly PeriodEnd, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record RecordObjectiveProgressRequest(DateOnly MeasuredOn, decimal Value, string? Comment);

public sealed record CloseObjectiveRequest(string Outcome, string Note);

public sealed record ObjectiveProgressDto(
    Guid Id, DateOnly MeasuredOn, decimal Value, Guid RecordedById, string? Comment);

public sealed record QualityObjectiveListItemDto(
    Guid Id, string ObjectiveRef, string Title, string Metric, string Unit,
    decimal TargetValue, string Direction, Guid OwnerId,
    DateOnly PeriodStart, DateOnly PeriodEnd, string Status,
    decimal? CurrentValue, bool? OnTarget, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record QualityObjectiveDetailDto(
    Guid Id, string ObjectiveRef, string Title, string? Description, string Metric, string Unit,
    decimal TargetValue, string Direction, Guid OwnerId,
    DateOnly PeriodStart, DateOnly PeriodEnd, string Status,
    decimal? CurrentValue, bool? OnTarget, string? ClosureNote,
    Guid? BranchId, Guid? DepartmentId,
    IReadOnlyList<ObjectiveProgressDto> Updates);

// ── General Feedback & Satisfaction (ISO 17025 §8.6.2 / ISO 15189 §8.6) ─────

public sealed record LogFeedbackRequest(
    string Source, string Channel, string Type, string Subject, string Details,
    int? SatisfactionScore, DateOnly ReceivedOn, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record ReviewFeedbackRequest(string ReviewNotes);

public sealed record CloseFeedbackRequest(string ActionSummary);

public sealed record EscalateFeedbackRequest(string ComplainantName, string? ComplainantContact);

public sealed record FeedbackListItemDto(
    Guid Id, string FeedbackRef, string Source, string Channel, string Type, string Subject,
    int? SatisfactionScore, DateOnly ReceivedOn, string Status,
    Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record FeedbackDetailDto(
    Guid Id, string FeedbackRef, string Source, string Channel, string Type,
    string Subject, string Details, int? SatisfactionScore, DateOnly ReceivedOn,
    Guid LoggedBy, string Status, string? ReviewNotes, string? ActionSummary,
    Guid? ComplaintId, Guid? BranchId, Guid? DepartmentId);
