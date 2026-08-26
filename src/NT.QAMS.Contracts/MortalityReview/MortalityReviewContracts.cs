namespace NT.QAMS.Contracts.MortalityReview;

// ── Mortality reviews ─────────────────────────────────────────────────────────

public sealed record ReportMortalityRequest(
    string PatientRef, string Unit, DateTimeOffset DeathDateUtc, string? PrimaryDiagnosis, Guid? DepartmentId);

public sealed record ClassifyMortalityRequest(string Classification, string Findings);

public sealed record SecondReviewRequest(string Notes, bool Concurs);

public sealed record CommitteeDiscussedRequest(string Learnings);

public sealed record MortalityListItemDto(
    Guid Id, string ReviewRef, string PatientRef, string Unit, DateTimeOffset DeathDateUtc,
    string? Classification, bool RequiresSecondReview, string Status);

public sealed record MortalityDetailDto(
    Guid Id, string ReviewRef, string PatientRef, string Unit, Guid? DepartmentId, DateTimeOffset DeathDateUtc,
    string? PrimaryDiagnosis, string Status, string? Classification, bool RequiresSecondReview,
    Guid? FirstReviewerId, string? ClassificationFindings,
    Guid? SecondReviewerId, string? SecondReviewNotes, bool? SecondReviewerConcurs, string? CommitteeLearnings);

// ── Complication register (morbidity) ─────────────────────────────────────────

public sealed record ReportComplicationRequest(
    string PatientRef, string Unit, string Type, string Severity, DateTimeOffset OccurredDateUtc,
    string Description, Guid? DepartmentId);

public sealed record ReviewComplicationRequest(string Notes, bool Preventable);

public sealed record ComplicationListItemDto(
    Guid Id, string CaseRef, string PatientRef, string Unit, string Type, string Severity,
    DateTimeOffset OccurredDateUtc, bool? Preventable, string Status);

public sealed record ComplicationDetailDto(
    Guid Id, string CaseRef, string PatientRef, string Unit, Guid? DepartmentId, string Type, string Severity,
    DateTimeOffset OccurredDateUtc, string Description, string Status,
    Guid? ReviewedBy, string? ReviewNotes, bool? Preventable, DateTimeOffset? ReviewedAtUtc);

// ── Rates & summary (mortality per 1,000 patient-days from the M24 denominator) ─

public sealed record MortalityRatesDto(
    DateTimeOffset FromUtc, DateTimeOffset ToUtc, int PatientDays,
    int Deaths, decimal MortalityRatePer1000,
    int Expected, int Unexpected, int PotentiallyPreventable, int Preventable,
    int Complications, int PreventableComplications);
