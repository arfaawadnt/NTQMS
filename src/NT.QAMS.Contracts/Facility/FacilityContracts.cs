namespace NT.QAMS.Contracts.Facility;

// ── Environmental & Facility Monitoring (ISO 17025 §6.3) ────────────────────

public sealed record RegisterMonitoringPointRequest(
    string Name, string? Location, string Parameter, string Unit,
    decimal? LowLimit, decimal? HighLimit, Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record SetMonitoringLimitsRequest(decimal? LowLimit, decimal? HighLimit);

public sealed record RecordReadingRequest(decimal Value, string? Remark);

public sealed record EnvironmentalReadingDto(
    Guid Id, decimal Value, DateTimeOffset RecordedAtUtc, Guid RecordedById, bool InLimit, string? Remark);

public sealed record MonitoringPointListItemDto(
    Guid Id, string PointRef, string Name, string? Location, string Parameter, string Unit,
    decimal? LowLimit, decimal? HighLimit, string Status,
    decimal? LastValue, DateTimeOffset? LastRecordedAtUtc, bool? LastInLimit, int ExcursionCount,
    Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record MonitoringPointDetailDto(
    Guid Id, string PointRef, string Name, string? Location, string Parameter, string Unit,
    decimal? LowLimit, decimal? HighLimit, string Status,
    Guid? BranchId, Guid? DepartmentId,
    IReadOnlyList<EnvironmentalReadingDto> Readings);
