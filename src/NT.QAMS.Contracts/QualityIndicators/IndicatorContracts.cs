namespace NT.QAMS.Contracts.QualityIndicators;

public sealed record DefineIndicatorRequest(
    string Code, string Name, string? Description,
    string Numerator, string Denominator, string Unit, decimal RateFactor,
    string Frequency, string Direction,
    string? Inclusions = null, string? Exclusions = null, string? DataSource = null);

public sealed record UpdateIndicatorDefinitionRequest(
    string Name, string? Description,
    string Numerator, string Denominator, string Unit, decimal RateFactor,
    string Frequency, string Direction,
    string? Inclusions, string? Exclusions, string? DataSource);

public sealed record SetIndicatorTargetsRequest(
    decimal? Target, decimal? WarningThreshold, decimal? ActionThreshold);

public sealed record RecordMeasurementRequest(
    DateOnly Period, decimal Numerator, decimal Denominator, string? Note = null);

public sealed record IndicatorMeasurementDto(
    Guid Id, DateOnly Period, decimal Numerator, decimal Denominator, decimal Value,
    string Status, Guid EnteredBy, DateTimeOffset RecordedAtUtc, string? Note);

public sealed record IndicatorListItemDto(
    Guid Id, string IndicatorRef, string Code, string Name, string Unit, string Frequency,
    string Direction, string Status, decimal? Target, decimal? LatestValue, string? LatestStatus,
    DateOnly? LatestPeriod);

public sealed record IndicatorDetailDto(
    Guid Id, string IndicatorRef, string Code, string Name, string? Description,
    string Numerator, string Denominator, string? Inclusions, string? Exclusions, string? DataSource,
    string Unit, decimal RateFactor, string Frequency, string Direction, string Status,
    decimal? Target, decimal? WarningThreshold, decimal? ActionThreshold,
    IReadOnlyList<IndicatorMeasurementDto> Measurements);

// ── Statistical process control ─────────────────────────────────────────────

public sealed record SpcPointDto(DateOnly Period, decimal Value, bool SpecialCause, IReadOnlyList<string> Rules);

public sealed record IndicatorControlChartDto(
    Guid IndicatorId, string Code, string Unit, bool HasLimits,
    decimal Mean, decimal StdDev, decimal Ucl, decimal Lcl,
    decimal Upper2Sigma, decimal Lower2Sigma, decimal Upper1Sigma, decimal Lower1Sigma,
    decimal? Target, decimal? WarningThreshold, decimal? ActionThreshold,
    IReadOnlyList<SpcPointDto> Points);
