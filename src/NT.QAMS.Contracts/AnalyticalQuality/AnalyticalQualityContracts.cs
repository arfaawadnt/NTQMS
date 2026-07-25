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

// ── Method Comparison (CLSI EP09) ───────────────────────────────────────────

public sealed record CreateMethodComparisonRequest(
    string Analyte, string Unit, string ReferenceMethod, string TestMethod);

public sealed record AddMeasurementPairRequest(decimal ReferenceValue, decimal TestValue, string? SampleId);

public sealed record MeasurementPairDto(Guid Id, decimal ReferenceValue, decimal TestValue, string? SampleId);

public sealed record MethodComparisonListItemDto(
    Guid Id, string StudyRef, string Analyte, string ReferenceMethod, string TestMethod, string State,
    int? PairCount, decimal? DemingSlope, decimal? DemingIntercept, decimal? PearsonR, decimal? MeanBias);

public sealed record MethodComparisonDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, string ReferenceMethod, string TestMethod, string State,
    int? PairCount, decimal? PearsonR, decimal? DemingSlope, decimal? DemingIntercept,
    decimal? PassingBablokSlope, decimal? PassingBablokIntercept,
    decimal? MeanBias, decimal? BiasSd, decimal? LimitOfAgreementLower, decimal? LimitOfAgreementUpper,
    bool MeetsRecommendedPower, Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<MeasurementPairDto> Pairs);

// ── Linearity / AMR (CLSI EP06) ─────────────────────────────────────────────

public sealed record CreateLinearityStudyRequest(
    string Analyte, string Unit, string Method, decimal AllowableDeviationPct);

public sealed record AddLinearityMeasurementRequest(decimal AssignedValue, decimal MeasuredValue);

public sealed record LinearityMeasurementDto(Guid Id, decimal AssignedValue, decimal MeasuredValue);

public sealed record LinearityLevelDto(
    decimal AssignedValue, int ReplicateCount, decimal MeanMeasured, decimal FittedValue,
    decimal DeviationPct, decimal RecoveryPct, bool Passes);

public sealed record LinearityListItemDto(
    Guid Id, string StudyRef, string Analyte, string Method, string State,
    bool? IsLinear, decimal? AmrLow, decimal? AmrHigh, decimal? Slope, decimal? CorrelationR);

public sealed record LinearityDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, string Method,
    decimal AllowableDeviationPct, string State,
    decimal? Slope, decimal? Intercept, decimal? CorrelationR, bool? IsLinear,
    decimal? AmrLow, decimal? AmrHigh, Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<LinearityMeasurementDto> Measurements,
    IReadOnlyList<LinearityLevelDto> Levels);

// ── Detection Capability: LoB / LoD / LoQ (CLSI EP17) ───────────────────────

public sealed record CreateDetectionLimitStudyRequest(
    string Analyte, string Unit, string Method, decimal LoqCvTargetPct);

public sealed record AddDetectionMeasurementRequest(string Kind, decimal? AssignedValue, decimal MeasuredValue);

public sealed record DetectionMeasurementDto(Guid Id, string Kind, decimal? AssignedValue, decimal MeasuredValue);

public sealed record LowLevelAssessmentDto(
    decimal AssignedValue, int ReplicateCount, decimal Mean, decimal Sd, decimal CvPct, bool QualifiesForLoq);

public sealed record DetectionLimitListItemDto(
    Guid Id, string StudyRef, string Analyte, string Method, string State,
    decimal? Lob, decimal? Lod, decimal? Loq);

public sealed record DetectionLimitDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, string Method,
    decimal LoqCvTargetPct, string State,
    decimal? BlankMean, decimal? BlankSd, decimal? PooledLowSd,
    decimal? Lob, decimal? Lod, decimal? Loq,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<DetectionMeasurementDto> Measurements,
    IReadOnlyList<LowLevelAssessmentDto> LowLevels);

// ── Reference-Interval Verification (CLSI EP28) ─────────────────────────────

public sealed record CreateReferenceIntervalStudyRequest(
    string Analyte, string Unit, string Population, string Source,
    decimal ClaimedLower, decimal ClaimedUpper);

public sealed record AddReferenceSampleRequest(decimal Value, string? SubjectRef);

public sealed record ReferenceSampleDto(Guid Id, decimal Value, string? SubjectRef, bool Outside);

public sealed record ReferenceIntervalListItemDto(
    Guid Id, string StudyRef, string Analyte, string Population,
    decimal ClaimedLower, decimal ClaimedUpper, string State,
    int? OutsideCount, int? AllowedOutside, string? Verdict);

public sealed record ReferenceIntervalDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, string Population, string Source,
    decimal ClaimedLower, decimal ClaimedUpper, string State,
    int? SampleCount, int? OutsideCount, int? AllowedOutside, string? Verdict,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<ReferenceSampleDto> Samples);

// ── PT/EQA Annual Plan (ISO 17025 §7.7.2) ───────────────────────────────────

public sealed record CreatePtPlanRequest(int Year);

public sealed record AddPtPlanItemRequest(
    string Scheme, string Analyte, string? Provider, int PlannedCycles, string? Notes);

public sealed record RecordPtPlanFulfilmentRequest(Guid ItemId, Guid EnrollmentId);

public sealed record ClosePtPlanRequest(string ClosureSummary);

public sealed record PtPlanItemDto(
    Guid Id, string Scheme, string Analyte, string? Provider,
    int PlannedCycles, int FulfilledCycles, string? LastEnrollmentRef, string? Notes);

public sealed record PtPlanListItemDto(
    Guid Id, string PlanRef, int Year, string Status,
    int ItemCount, int PlannedCycles, int FulfilledCycles);

public sealed record PtPlanDetailDto(
    Guid Id, string PlanRef, int Year, string Status,
    Guid? ApprovedBy, DateTimeOffset? ApprovedAtUtc, string? ClosureSummary,
    IReadOnlyList<PtPlanItemDto> Items);

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
