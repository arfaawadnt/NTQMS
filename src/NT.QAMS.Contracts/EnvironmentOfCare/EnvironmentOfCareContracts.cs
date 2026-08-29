namespace NT.QAMS.Contracts.EnvironmentOfCare;

// ── Safety rounds ─────────────────────────────────────────────────────────────

public sealed record ScheduleRoundRequest(string Area, string Type, DateOnly ScheduledDate);

public sealed record AddFindingRequest(string Description, string Severity);

public sealed record ResolveFindingRequest(string Note);

public sealed record RoundListItemDto(
    Guid Id, string RoundRef, string Area, string Type, DateOnly ScheduledDate, string Status,
    int OpenFindings, int TotalFindings);

public sealed record RoundFindingDto(
    Guid Id, string Description, string Severity, string Status, string? CorrectiveNote, DateTimeOffset? ResolvedAtUtc,
    string? RaisedNcRef);

public sealed record RoundDetailDto(
    Guid Id, string RoundRef, string Area, string Type, DateOnly ScheduledDate, string Status,
    Guid? ConductedBy, DateTimeOffset? CompletedAtUtc, IReadOnlyList<RoundFindingDto> Findings);

// ── Drills ────────────────────────────────────────────────────────────────────

public sealed record ScheduleDrillRequest(string Type, string Location, DateOnly ScheduledDate);

public sealed record ExecuteDrillRequest(DateTimeOffset ExecutedAtUtc, int ParticipantCount);

public sealed record EvaluateDrillRequest(int Score, string ImprovementNotes);

public sealed record DrillListItemDto(
    Guid Id, string DrillRef, string Type, string Location, DateOnly ScheduledDate, string Status,
    int? ParticipantCount, int? EvaluationScore, string? Effectiveness);

public sealed record DrillDetailDto(
    Guid Id, string DrillRef, string Type, string Location, DateOnly ScheduledDate, string Status,
    DateTimeOffset? ExecutedAtUtc, int? ParticipantCount, int? EvaluationScore, string? Effectiveness, string? ImprovementNotes);

// ── EOC summary dashboard ─────────────────────────────────────────────────────

public sealed record EocSummaryDto(
    int RoundsScheduled, int RoundsCompleted, int OpenFindings, int CriticalOpenFindings,
    int DrillsScheduled, int DrillsEvaluated, decimal? MeanDrillScore);
