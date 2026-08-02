namespace NT.QAMS.Contracts.Resources;

// ── Equipment & Calibration ──────────────────────────────────────────────────

public sealed record RegisterEquipmentRequest(
    string Name, string SerialNumber, string? Location,
    int CalibrationIntervalDays, int GracePeriodDays, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record LogCalibrationRequest(
    DateOnly PerformedAt, string Provider, string Result, Guid? CertificateFileId);

public sealed record LogMaintenanceRequest(DateOnly PerformedAt, string WorkDescription, Guid? CertificateFileId = null);

public sealed record CalibrationRecordDto(
    Guid Id, DateOnly PerformedAt, string Provider, string Result, Guid? CertificateFileId);

public sealed record MaintenanceRecordDto(Guid Id, DateOnly PerformedAt, string WorkDescription, Guid? CertificateFileId = null);

public sealed record EquipmentListItemDto(
    Guid Id, string Code, string Name, string SerialNumber, string? Location,
    string Status, DateOnly? NextCalibrationDue, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record RecordIntermediateCheckRequest(
    DateOnly PerformedOn, string CheckType, bool Passed, Guid? ReferenceStandardId, string? Remarks);

public sealed record IntermediateCheckDto(
    Guid Id, DateOnly PerformedOn, Guid PerformedById, string CheckType,
    bool Passed, Guid? ReferenceStandardId, string? Remarks);

public sealed record EquipmentDetailDto(
    Guid Id, string Code, string Name, string SerialNumber, string? Location,
    string Status, int CalibrationIntervalDays, int GracePeriodDays,
    DateOnly? LastCalibrationAt, DateOnly? NextCalibrationDue,
    IReadOnlyList<CalibrationRecordDto> Calibrations,
    IReadOnlyList<MaintenanceRecordDto> Maintenance,
    IReadOnlyList<IntermediateCheckDto> IntermediateChecks);

// ── Metrological Traceability (ISO 17025 §6.5) ──────────────────────────────

public sealed record RegisterReferenceStandardRequest(
    string Name, string Type, string TraceableTo,
    string? Manufacturer, string? LotNumber, string? CertificateNumber,
    string? CertifiedValue, string? UncertaintyStatement,
    DateOnly ReceivedOn, DateOnly? ExpiresOn, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record QuarantineReferenceStandardRequest(string Reason);

public sealed record ReferenceStandardListItemDto(
    Guid Id, string StandardRef, string Name, string Type, string TraceableTo,
    string Status, DateOnly? ExpiresOn, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record ReferenceStandardDetailDto(
    Guid Id, string StandardRef, string Name, string Type, string TraceableTo,
    string? Manufacturer, string? LotNumber, string? CertificateNumber,
    string? CertifiedValue, string? UncertaintyStatement,
    DateOnly ReceivedOn, DateOnly? ExpiresOn, string Status, string? QuarantineReason,
    Guid? BranchId, Guid? DepartmentId);

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

// ── Personnel Authorization Matrix (ISO 17025 §6.2.6) ───────────────────────

public sealed record GrantTestAuthorizationRequest(
    Guid UserId, Guid TestCatalogItemId, Guid CompetencyRecordId, string Scope);

public sealed record SuspendTestAuthorizationRequest(string Reason);

public sealed record RevokeTestAuthorizationRequest(string Reason);

public sealed record TestAuthorizationListItemDto(
    Guid Id, Guid UserId, Guid TestCatalogItemId, string TestCode, string TestName,
    string Scope, string Status, DateOnly GrantedOn, DateOnly ExpiresOn);

public sealed record TestAuthorizationDetailDto(
    Guid Id, Guid UserId, Guid TestCatalogItemId, string TestCode, string TestName,
    Guid CompetencyRecordId, string? CompetencySubject, string Scope, string Status,
    Guid GrantedBy, DateOnly GrantedOn, DateOnly ExpiresOn,
    string? SuspensionReason, string? RevocationReason);

public sealed record AssignTrainingRequest(
    Guid TraineeId, string Subject, Guid? DocumentId, DateOnly DueDate);

public sealed record TrainingAssignmentDto(
    Guid Id, Guid TraineeId, string Subject, Guid? DocumentId, DateOnly DueDate,
    bool Completed, DateTimeOffset? CompletedAtUtc);
