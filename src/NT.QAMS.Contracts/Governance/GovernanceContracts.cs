namespace NT.QAMS.Contracts.Governance;

// ── Risk ─────────────────────────────────────────────────────────────────────

public sealed record AssessRiskRequest(string Title, string Category, int Likelihood, int Impact, Guid? BranchId = null, Guid? DepartmentId = null);
public sealed record AddMitigationRequest(string Description, Guid OwnerId, DateOnly DueDate);
public sealed record ResidualAssessmentRequest(int Likelihood, int Impact);

public sealed record MitigationActionDto(
    Guid Id, string Description, Guid OwnerId, DateOnly DueDate, bool Completed);

public sealed record RiskListItemDto(
    Guid Id, string RiskRef, string Title, string Category, string Status, int Rpn, int? ResidualRpn, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record RiskDetailDto(
    Guid Id, string RiskRef, string Title, string Category, string Status,
    int Likelihood, int Impact, int Rpn,
    int? ResidualLikelihood, int? ResidualImpact, int? ResidualRpn,
    IReadOnlyList<MitigationActionDto> Actions);

// ── Change control ───────────────────────────────────────────────────────────

public sealed record ProposeChangeRequest(
    string Title, string ImpactAnalysis, string ImpactLevel = "Medium", Guid? BranchId = null, Guid? DepartmentId = null);
/// <summary>Raise an already-implemented emergency change for retrospective ratification by a deadline (HQMS M18).</summary>
public sealed record ProposeEmergencyChangeRequest(
    string Title, string ImpactAnalysis, DateOnly RetrospectiveDeadline, Guid? BranchId = null, Guid? DepartmentId = null);
public sealed record LinkRiskRequest(Guid RiskItemId);
public sealed record RejectChangeRequest(string Reason);
/// <summary>The two 21 CFR Part 11 identification components (§11.200(a)(1)) to approve a change.</summary>
public sealed record ApproveChangeRequest(string Password, string Pin);
/// <summary>Part 11 credentials plus the implementation notes to ratify an emergency change.</summary>
public sealed record RatifyChangeRequest(string ImplementationNotes, string Password, string Pin);
public sealed record CloseChangeRequest(string ImplementationNotes);
public sealed record ReviewChangeRequest(bool Effective, string Notes);

public sealed record ChangeListItemDto(
    Guid Id, string ChangeRef, string Title, string Status, Guid? RiskItemId,
    string ImpactLevel = "Medium", bool IsEmergency = false, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record ChangeDetailDto(
    Guid Id, string ChangeRef, string Title, string ImpactAnalysis, string Status,
    Guid ProposedBy, Guid? RiskItemId, Guid? ApprovedBy, DateTimeOffset? ApprovedAtUtc,
    string? RejectionReason, string? ImplementationNotes,
    bool? ChangeEffective = null, string? PostImplementationReviewNotes = null,
    Guid? PostImplementationReviewedBy = null, DateTimeOffset? PostImplementationReviewedAtUtc = null,
    string ImpactLevel = "Medium", bool IsEmergency = false, DateOnly? RetrospectiveDeadline = null,
    Guid? RatifiedBy = null, DateTimeOffset? RatifiedAtUtc = null);

// ── Management review ────────────────────────────────────────────────────────

/// <summary>
/// Participants are user ids: the display names for the minutes are resolved
/// server-side, and the invitation is mailed to each participant. Leave
/// <paramref name="MeetingLink"/> empty to have one generated.
/// </summary>
public sealed record ScheduleReviewRequest(string Title, DateOnly ReviewDate,
    IReadOnlyList<Guid> ParticipantUserIds,
    string? Agenda = null, string? MeetingLink = null,
    Guid? BranchId = null, Guid? DepartmentId = null);
public sealed record AddDecisionRequest(string Description, Guid OwnerId, DateOnly DueDate);
public sealed record CloseReviewRequest(string Minutes, string Password, string Pin);

public sealed record ReviewDecisionDto(Guid Id, string Description, Guid OwnerId, DateOnly DueDate);

public sealed record ReviewListItemDto(
    Guid Id, string ReviewRef, string Title, DateOnly ReviewDate, string Status, int DecisionCount, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record ReviewDetailDto(
    Guid Id, string ReviewRef, string Title, DateOnly ReviewDate, string Participants,
    string Status, string? Minutes, Guid? ClosedBy, IReadOnlyList<ReviewDecisionDto> Decisions,
    string? Agenda = null, string? MeetingLink = null);

// ── Supplier quality ─────────────────────────────────────────────────────────

public sealed record RegisterSupplierRequest(
    string Name, string SupplierType, bool IsOutsourcedClinicalService = false, string? ServiceScope = null,
    Guid? BranchId = null, Guid? DepartmentId = null);
public sealed record AddCertificateRequest(string CertificateType, DateOnly ExpiresAt, Guid? FileId);
public sealed record SuspendSupplierRequest(string Reason);
/// <summary>The two 21 CFR Part 11 identification components (§11.200(a)(1)) to approve a supplier.</summary>
public sealed record ApproveSupplierRequest(string Password, string Pin);
public sealed record EvaluationCriterionRequest(string Criterion, decimal Weight, decimal Score);
public sealed record RecordEvaluationRequest(
    DateOnly PeriodStart, DateOnly PeriodEnd, IReadOnlyList<EvaluationCriterionRequest> Criteria);

// ── Contract / SLA register & corrective-action requests (HQMS M16) ───────────

public sealed record AddContractRequest(string Title, DateOnly StartDate, DateOnly EndDate, string? SlaSummary);
public sealed record TerminateContractRequest(string Reason);
public sealed record RaiseSupplierCarRequest(string Description, DateOnly RaisedOn, DateOnly? DueDate);
public sealed record RecordCarResponseRequest(string Note, DateOnly On);
public sealed record CloseSupplierCarRequest(bool Effective, string ClosureNote);

public sealed record CertificateDto(Guid Id, string CertificateType, DateOnly ExpiresAt, Guid? FileId);
public sealed record SupplierContractDto(
    Guid Id, string ContractRef, string Title, DateOnly StartDate, DateOnly EndDate, string? SlaSummary,
    string Status, string? TerminationReason, bool IsExpired);
public sealed record SupplierCarDto(
    Guid Id, string Description, DateOnly RaisedOn, DateOnly? DueDate, string Status, string? ResponseNote,
    DateOnly? ResponseOn, bool? Effective, string? ClosureNote, bool IsOverdue);
public sealed record OutsourcedServiceDto(
    Guid Id, string SupplierRef, string Name, string? ServiceScope, string Status,
    int ActiveContracts, int OpenCars, decimal? LatestEvaluationScore);

public sealed record SupplierListItemDto(
    Guid Id, string SupplierRef, string Name, string SupplierType, string Status,
    bool IsOutsourcedClinicalService = false, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record SupplierDetailDto(
    Guid Id, string SupplierRef, string Name, string SupplierType, string Status,
    Guid RegisteredBy, Guid? ApprovedBy, string? SuspensionReason,
    IReadOnlyList<CertificateDto> Certificates,
    bool IsOutsourcedClinicalService = false, string? ServiceScope = null,
    IReadOnlyList<SupplierContractDto>? Contracts = null,
    IReadOnlyList<SupplierCarDto>? Cars = null);

public sealed record SupplierEvaluationDto(
    Guid Id, Guid SupplierId, DateOnly PeriodStart, DateOnly PeriodEnd,
    decimal WeightedTotal, Guid EvaluatedBy, string Criteria);

// ── Impartiality / Conflict-of-Interest Register (ISO 17025 §4.1) ───────────

public sealed record DeclareConflictRequest(
    Guid DeclarantId, string Description, string RelatedParty, DateOnly DeclaredOn);

public sealed record AssessConflictRequest(string RiskLevel, string Mitigation, string Password, string Pin);

public sealed record CloseConflictRequest(string Outcome, string ClosureNote);

public sealed record ConflictListItemDto(
    Guid Id, string ConflictRef, Guid DeclarantId, string RelatedParty, DateOnly DeclaredOn,
    string Status, string? RiskLevel, string? Outcome);

public sealed record ConflictDetailDto(
    Guid Id, string ConflictRef, Guid DeclarantId, string Description, string RelatedParty,
    DateOnly DeclaredOn, string Status, string? RiskLevel, string? Mitigation, Guid? AssessedBy,
    string? Outcome, string? ClosureNote);
