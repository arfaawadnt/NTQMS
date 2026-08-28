namespace NT.QAMS.Contracts.InfectionControl;

// ── HAI cases ─────────────────────────────────────────────────────────────────

public sealed record ReportHaiCaseRequest(
    string Type, string PatientRef, string Unit, DateTimeOffset OnsetDateUtc,
    string? Organism, string Description, Guid? DepartmentId);

public sealed record ReviewHaiCaseRequest(string Notes);
public sealed record RejectHaiCaseRequest(string Reason);

public sealed record HaiCaseListItemDto(
    Guid Id, string CaseRef, string Type, string PatientRef, string Unit, DateTimeOffset OnsetDateUtc,
    string? Organism, string Status);

public sealed record HaiCaseDetailDto(
    Guid Id, string CaseRef, string Type, string PatientRef, string Unit, Guid? DepartmentId,
    DateTimeOffset OnsetDateUtc, string? Organism, string Description, string Status,
    Guid? ReviewedBy, string? ReviewNotes, DateTimeOffset? ReviewedAtUtc);

// ── Device exposures (the device-day denominator) ─────────────────────────────

public sealed record RecordDeviceExposureRequest(
    string PatientRef, string Unit, string DeviceType, DateTimeOffset InsertedAtUtc, Guid? DepartmentId);

public sealed record RemoveDeviceRequest(DateTimeOffset RemovedAtUtc);

public sealed record DeviceExposureListItemDto(
    Guid Id, string PatientRef, string Unit, string DeviceType,
    DateTimeOffset InsertedAtUtc, DateTimeOffset? RemovedAtUtc, string Status);

// ── Rates (per 1,000 device-days; utilisation vs the M24 patient-days) ────────

/// <summary>
/// One device-associated infection rate: cases per 1,000 device-days, plus the device-utilisation
/// ratio (device-days ÷ patient-days) that contextualises it.
/// </summary>
public sealed record HaiDeviceRateDto(
    string HaiType, string DeviceType, int DeviceDays, int CaseCount, decimal? RatePer1000, decimal? UtilizationRatio);

public sealed record HaiRatesDto(
    DateTimeOffset FromUtc, DateTimeOffset ToUtc, int PatientDays,
    HaiDeviceRateDto Clabsi, HaiDeviceRateDto Cauti, HaiDeviceRateDto Vap, int SsiCount);
