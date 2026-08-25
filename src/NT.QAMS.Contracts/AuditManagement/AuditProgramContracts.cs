namespace NT.QAMS.Contracts.AuditManagement;

public sealed record CreateAuditProgramRequest(int Year, string Title);

public sealed record AddPlannedAuditRequest(
    string ScopeArea, Guid? DepartmentId, string? StandardChapter, string Priority, int PlannedQuarter);

public sealed record LinkScheduledAuditRequest(Guid AuditId);

public sealed record CompletePlannedAuditRequest(DateOnly CompletedOn);

public sealed record PlannedAuditDto(
    Guid Id, string ScopeArea, Guid? DepartmentId, string? StandardChapter, string Priority,
    int PlannedQuarter, string Status, Guid? ScheduledAuditId, DateOnly? CompletedOn);

public sealed record AuditProgramCoverageDto(
    int Planned, int Scheduled, int Completed, decimal CoveragePercent, decimal ScheduledPercent);

public sealed record AuditProgramListItemDto(
    Guid Id, int Year, string Title, string Status, int PlannedCount, decimal CoveragePercent);

public sealed record AuditProgramDetailDto(
    Guid Id, int Year, string Title, string Status,
    AuditProgramCoverageDto Coverage, IReadOnlyList<PlannedAuditDto> Plan);
