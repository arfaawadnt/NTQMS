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

// ── Measurement uncertainty (ISO 17025 §7.6) ─────────────────────────────────

public sealed record CreateUncertaintyBudgetRequest(
    string Analyte, string Method, string Unit, string Level,
    decimal CoverageFactor, decimal? TargetExpandedUncertainty);
public sealed record AddUncertaintyComponentRequest(
    string Name, string Type, decimal RelativeStandardUncertainty, string? Source);

public sealed record UncertaintyComponentDto(
    Guid Id, string Name, string Type, decimal RelativeStandardUncertainty, string? Source);

public sealed record UncertaintyBudgetListItemDto(
    Guid Id, string BudgetRef, string Analyte, string Method, string Level, string Status,
    decimal? ExpandedUncertainty, bool? MeetsTarget);

public sealed record UncertaintyBudgetDetailDto(
    Guid Id, string BudgetRef, string Analyte, string Method, string Unit, string Level,
    decimal CoverageFactor, decimal? TargetExpandedUncertainty, string Status,
    decimal? CombinedStandardUncertainty, decimal? ExpandedUncertainty, bool? MeetsTarget,
    Guid? ApprovedBy, DateTimeOffset? ApprovedAtUtc,
    IReadOnlyList<UncertaintyComponentDto> Components);
