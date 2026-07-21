namespace NT.QAMS.Contracts.Resources;

// ── Equipment & Calibration ──────────────────────────────────────────────────

public sealed record RegisterEquipmentRequest(
    string Name, string SerialNumber, string? Location,
    int CalibrationIntervalDays, int GracePeriodDays);

public sealed record LogCalibrationRequest(
    DateOnly PerformedAt, string Provider, string Result, Guid? CertificateFileId);

public sealed record LogMaintenanceRequest(DateOnly PerformedAt, string WorkDescription);

public sealed record CalibrationRecordDto(
    Guid Id, DateOnly PerformedAt, string Provider, string Result, Guid? CertificateFileId);

public sealed record MaintenanceRecordDto(Guid Id, DateOnly PerformedAt, string WorkDescription);

public sealed record EquipmentListItemDto(
    Guid Id, string Code, string Name, string SerialNumber, string? Location,
    string Status, DateOnly? NextCalibrationDue);

public sealed record EquipmentDetailDto(
    Guid Id, string Code, string Name, string SerialNumber, string? Location,
    string Status, int CalibrationIntervalDays, int GracePeriodDays,
    DateOnly? LastCalibrationAt, DateOnly? NextCalibrationDue,
    IReadOnlyList<CalibrationRecordDto> Calibrations,
    IReadOnlyList<MaintenanceRecordDto> Maintenance);

// ── Competency & Training ────────────────────────────────────────────────────

public sealed record AssignCompetencyRequest(
    Guid TraineeId, string Subject, Guid? DocumentId, int ValidityMonths);

public sealed record ScoreAssessmentRequest(int Score);

public sealed record RevokeCompetencyRequest(string Reason);

public sealed record AssessmentResultDto(Guid Id, int Score, Guid AssessorId, DateTimeOffset AssessedAtUtc);

public sealed record CompetencyListItemDto(
    Guid Id, Guid TraineeId, string Subject, string Status, DateOnly? ExpiresAt);

public sealed record CompetencyDetailDto(
    Guid Id, Guid TraineeId, string Subject, Guid? DocumentId, string Status,
    int ValidityMonths, DateOnly? ExpiresAt, Guid? AuthorizedBy, string? RevocationReason,
    IReadOnlyList<AssessmentResultDto> Assessments);

public sealed record AssignTrainingRequest(
    Guid TraineeId, string Subject, Guid? DocumentId, DateOnly DueDate);

public sealed record TrainingAssignmentDto(
    Guid Id, Guid TraineeId, string Subject, Guid? DocumentId, DateOnly DueDate,
    bool Completed, DateTimeOffset? CompletedAtUtc);
