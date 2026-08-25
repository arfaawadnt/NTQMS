namespace NT.QAMS.Contracts.RiskGovernance;

public sealed record CreateFmeaRequest(string Title, string ProcessName, string Type, Guid? BranchId, Guid? DepartmentId);

public sealed record AddFailureModeRequest(
    string ProcessStep, string FailureMode, string Effect, string Cause,
    int Severity, int Occurrence, int Detection);

public sealed record RecommendActionRequest(string Action, Guid? OwnerId);

public sealed record RecordResidualRequest(int Severity, int Occurrence, int Detection);

public sealed record FailureModeDto(
    Guid Id, string ProcessStep, string FailureMode, string Effect, string Cause,
    int Severity, int Occurrence, int Detection, int Rpn,
    string? RecommendedAction, Guid? ActionOwnerId,
    int? ResidualSeverity, int? ResidualOccurrence, int? ResidualDetection, int? ResidualRpn,
    string Status);

public sealed record FmeaListItemDto(
    Guid Id, string FmeaRef, string Title, string ProcessName, string Type, string Status,
    int FailureModeCount, int HighRpnCount, int MaxRpn);

public sealed record FmeaDetailDto(
    Guid Id, string FmeaRef, string Title, string ProcessName, string Type, string Status,
    Guid? BranchId, Guid? DepartmentId, int HighRpnThreshold,
    IReadOnlyList<FailureModeDto> FailureModes);
