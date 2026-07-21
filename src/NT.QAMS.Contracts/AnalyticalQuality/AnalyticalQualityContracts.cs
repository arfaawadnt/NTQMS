namespace NT.QAMS.Contracts.AnalyticalQuality;

// ── QC ───────────────────────────────────────────────────────────────────────

public sealed record CreateQcProfileRequest(
    string Analyte, string Instrument, string ControlLot, decimal TargetMean, decimal TargetSd);
public sealed record RecordQcRunRequest(decimal Value, string Operator);
public sealed record QcTroubleshootRequest(string Note);

public sealed record QcProfileDto(
    Guid Id, string Analyte, string Instrument, string ControlLot,
    decimal TargetMean, decimal TargetSd, bool IsActive);
public sealed record QcRunDto(
    Guid Id, Guid ProfileId, decimal Value, decimal ZScore, string Outcome,
    string ViolatedRules, string Operator, DateTimeOffset MeasuredAtUtc, string? TroubleshootingNote);

// ── Method validation ────────────────────────────────────────────────────────

public sealed record ConfigureStudyRequest(string Analyte, string Protocol, decimal TotalAllowableError);
public sealed record EnterReplicateRequest(string Level, decimal Measured, decimal? Reference);

public sealed record ReplicateDto(Guid Id, string Level, decimal Measured, decimal? Reference);
public sealed record ValidationStudyListItemDto(
    Guid Id, string StudyRef, string Analyte, string Protocol, string State, bool? Passed);
public sealed record ValidationStudyDetailDto(
    Guid Id, string StudyRef, string Analyte, string Protocol, decimal TotalAllowableError,
    string State, decimal? MeanBias, decimal? Cv, bool? Passed,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc, IReadOnlyList<ReplicateDto> Replicates);

// ── Proficiency testing ──────────────────────────────────────────────────────

public sealed record EnrollPtRequest(string Scheme, string Analyte, string Cycle);
public sealed record RecordPtResultRequest(decimal Submitted, decimal Assigned, decimal StandardDeviation);

public sealed record PtEnrollmentDto(
    Guid Id, string PtRef, string Scheme, string Analyte, string Cycle,
    decimal? SubmittedValue, decimal? AssignedValue, decimal? ZScore, string Performance);
