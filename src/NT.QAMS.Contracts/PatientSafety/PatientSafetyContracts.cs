namespace NT.QAMS.Contracts.PatientSafety;

public sealed record ReportFallRequest(
    string PatientRef, string Unit, DateTimeOffset OccurredAtUtc, string Harm, string Description, Guid? DepartmentId);

public sealed record ReportPressureInjuryRequest(
    string PatientRef, string Unit, DateTimeOffset OccurredAtUtc, string Harm, string Description,
    string Stage, string Origin, Guid? DepartmentId);

public sealed record ReviewSafetyEventRequest(string Notes);

public sealed record SafetyEventListItemDto(
    Guid Id, string EventRef, string Type, string PatientRef, string Unit, DateTimeOffset OccurredAtUtc,
    string HarmLevel, string Origin, string? Stage, string Status);

public sealed record SafetyEventDetailDto(
    Guid Id, string EventRef, string Type, string PatientRef, string Unit, Guid? DepartmentId,
    DateTimeOffset OccurredAtUtc, string HarmLevel, string Origin, string? Stage, string Description,
    string Status, Guid? ReviewedBy, string? ReviewNotes, DateTimeOffset? ReviewedAtUtc);

// ── Rates (per 1,000 patient-days, using the M24 ADT denominator) ─────────────

public sealed record SafetyRateDto(string Type, int EventCount, int PatientDays, decimal RatePer1000);

public sealed record SafetyRatesDto(
    DateTimeOffset FromUtc, DateTimeOffset ToUtc, int PatientDays,
    SafetyRateDto Falls, SafetyRateDto PressureInjuries,
    int HospitalAcquiredPressureInjuries, decimal HapiRatePer1000);
