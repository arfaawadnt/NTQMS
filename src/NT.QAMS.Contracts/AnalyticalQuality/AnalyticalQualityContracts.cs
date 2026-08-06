namespace NT.QAMS.Contracts.AnalyticalQuality;

/// <summary>
/// The two 21 CFR Part 11 identification components (§11.200(a)(1)) an analyst supplies
/// to sign off / approve an analytical study: the account password and the e-signature PIN.
/// Shared by every analytical sign-off endpoint.
/// </summary>
public sealed record AnalyticalSignOffRequest(string Password, string Pin);

// ── QC ───────────────────────────────────────────────────────────────────────

public sealed record CreateQcProfileRequest(
    string Analyte, string Instrument, string ControlLot, decimal TargetMean, decimal TargetSd);
public sealed record RecordQcRunRequest(decimal Value, string Operator);
public sealed record QcTroubleshootRequest(string Note);
public sealed record UpdateQcTargetsRequest(decimal TargetMean, decimal TargetSd, string Reason);

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

// ── Bulk import (LIS / analyzer CSV) ────────────────────────────────────────

public sealed record BulkRejectDto(int Row, string Reason);

public sealed record BulkImportResultDto(int Imported, IReadOnlyList<BulkRejectDto> Rejected);

public sealed record ImportMeasurementPairsRequest(IReadOnlyList<AddMeasurementPairRequest> Rows);

public sealed record ImportPrecisionMeasurementsRequest(IReadOnlyList<AddPrecisionMeasurementRequest> Rows);

// ── Outlier Detection & Normalisation ───────────────────────────────────────

public sealed record CreateOutlierScreeningRequest(string Dataset, string Unit);
public sealed record AddOutlierPointRequest(decimal Value, string? Label);
public sealed record OutlierPointDto(Guid Id, decimal Value, string? Label, decimal ZScore, decimal ModifiedZScore, bool IsOutlier);
public sealed record OutlierScreeningListItemDto(Guid Id, string ScreeningRef, string Dataset, string State, int? PointCount, int? OutlierCount);
public sealed record OutlierScreeningDetailDto(
    Guid Id, string ScreeningRef, string Dataset, string Unit, string State,
    int? PointCount, decimal? Mean, decimal? Sd, decimal? Median, decimal? Q1, decimal? Q3,
    decimal? TukeyLower, decimal? TukeyUpper, int? OutlierCount,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc, IReadOnlyList<OutlierPointDto> Points);

// ── Carryover (CLSI EP10) ───────────────────────────────────────────────────

public sealed record CreateCarryoverStudyRequest(string Analyte, string Unit, decimal AllowableCarryoverPct);
public sealed record AddCarryoverReadingRequest(string Kind, int Sequence, decimal Value);
public sealed record CarryoverReadingDto(Guid Id, string Kind, int Sequence, decimal Value);
public sealed record CarryoverListItemDto(Guid Id, string StudyRef, string Analyte, string State, decimal? CarryoverPct, bool? Passes);
public sealed record CarryoverDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, decimal AllowableCarryoverPct, string State,
    decimal? MeanHigh, decimal? FirstLow, decimal? SteadyLow, decimal? CarryoverPct, bool? Passes,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc, IReadOnlyList<CarryoverReadingDto> Readings);

// ── Lot-to-Lot Comparison ───────────────────────────────────────────────────

public sealed record CreateLotComparisonRequest(string Analyte, string Unit, string CurrentLot, string NewLot, decimal AllowableBiasPct);
public sealed record AddLotPairRequest(decimal CurrentLotValue, decimal NewLotValue, string? SampleId);
public sealed record LotPairDto(Guid Id, decimal CurrentLotValue, decimal NewLotValue, string? SampleId);
public sealed record LotComparisonListItemDto(Guid Id, string StudyRef, string Analyte, string CurrentLot, string NewLot, string State, decimal? MeanBiasPct, bool? Passes);
public sealed record LotComparisonDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, string CurrentLot, string NewLot, decimal AllowableBiasPct, string State,
    int? PairCount, decimal? MeanCurrent, decimal? MeanNew, decimal? MeanBiasPct, bool? Passes,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc, IReadOnlyList<LotPairDto> Pairs);

// ── Interference / Specificity (CLSI EP07) ──────────────────────────────────

public sealed record CreateInterferenceStudyRequest(string Analyte, string Unit, decimal AllowableBiasPct);
public sealed record AddInterferenceMeasurementRequest(string Kind, string? Interferent, decimal Value);
public sealed record InterferenceMeasurementDto(Guid Id, bool IsControl, string? Interferent, decimal Value);
public sealed record InterferenceResultDto(string Interferent, int ReplicateCount, decimal MeanTest, decimal BiasPct, bool SignificantInterference);
public sealed record InterferenceListItemDto(Guid Id, string StudyRef, string Analyte, string State, int? InterferentCount, int? SignificantCount);
public sealed record InterferenceDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, decimal AllowableBiasPct, string State,
    decimal? ControlMean, int? InterferentCount, int? SignificantCount,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<InterferenceMeasurementDto> Measurements, IReadOnlyList<InterferenceResultDto> Results);

// ── Instrument-to-Instrument Comparability ──────────────────────────────────

public sealed record CreateInstrumentComparabilityRequest(string Analyte, string Unit, string ReferenceInstrument, decimal AllowableBiasPct);
public sealed record AddInstrumentReadingRequest(string Instrument, string SampleId, decimal Value);
public sealed record InstrumentReadingDto(Guid Id, string Instrument, string SampleId, decimal Value);
public sealed record InstrumentResultDto(string Instrument, int PairedSamples, decimal MeanBiasPct, bool Comparable);
public sealed record InstrumentComparabilityListItemDto(Guid Id, string StudyRef, string Analyte, string ReferenceInstrument, string State, int? InstrumentCount, int? NonComparableCount);
public sealed record InstrumentComparabilityDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, string ReferenceInstrument, decimal AllowableBiasPct, string State,
    int? InstrumentCount, int? NonComparableCount, Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<InstrumentReadingDto> Readings, IReadOnlyList<InstrumentResultDto> Results);

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

// ── Sigma Metrics ───────────────────────────────────────────────────────────

public sealed record CreateSigmaAssessmentRequest(
    string Analyte, string Unit, decimal AllowableTotalErrorPct, decimal BiasPct, decimal CvPct);

public sealed record UpdateSigmaInputsRequest(decimal AllowableTotalErrorPct, decimal BiasPct, decimal CvPct);

public sealed record SigmaAssessmentListItemDto(
    Guid Id, string AssessmentRef, string Analyte,
    decimal AllowableTotalErrorPct, decimal BiasPct, decimal CvPct,
    decimal SigmaValue, string Grade, string State);

public sealed record SigmaAssessmentDetailDto(
    Guid Id, string AssessmentRef, string Analyte, string Unit,
    decimal AllowableTotalErrorPct, decimal BiasPct, decimal CvPct,
    decimal SigmaValue, string Grade, string QcRecommendation, string State,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc);

// ── Precision Study (CLSI EP05) ─────────────────────────────────────────────

public sealed record CreatePrecisionStudyRequest(
    string Analyte, string Unit, string Level,
    decimal? ClaimedRepeatabilityCvPct, decimal? ClaimedWithinLabCvPct);

public sealed record AddPrecisionMeasurementRequest(string RunLabel, decimal Value);

public sealed record PrecisionMeasurementDto(Guid Id, string RunLabel, decimal Value);

public sealed record PrecisionRunDto(string RunLabel, int ReplicateCount, decimal Mean);

public sealed record PrecisionListItemDto(
    Guid Id, string StudyRef, string Analyte, string Level, string State,
    decimal? RepeatabilityCvPct, decimal? WithinLabCvPct, bool? MeetsWithinLabClaim);

public sealed record PrecisionDetailDto(
    Guid Id, string StudyRef, string Analyte, string Unit, string Level,
    decimal? ClaimedRepeatabilityCvPct, decimal? ClaimedWithinLabCvPct, string State,
    decimal? GrandMean, decimal? RepeatabilitySd, decimal? RepeatabilityCvPct,
    decimal? BetweenRunSd, decimal? BetweenRunCvPct, decimal? WithinLabSd, decimal? WithinLabCvPct,
    bool? MeetsRepeatabilityClaim, bool? MeetsWithinLabClaim,
    Guid? SignedOffBy, DateTimeOffset? SignedOffAtUtc,
    IReadOnlyList<PrecisionMeasurementDto> Measurements,
    IReadOnlyList<PrecisionRunDto> Runs);

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
