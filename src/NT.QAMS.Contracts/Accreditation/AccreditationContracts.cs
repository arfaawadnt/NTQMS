namespace NT.QAMS.Contracts.Accreditation;

public sealed record DefineStandardSetRequest(string Framework, string Name, string Version);

public sealed record AddStandardElementRequest(
    string ChapterCode, string ChapterTitle, string StandardCode, string ElementCode, string Text, int Weight);

public sealed record AssessElementRequest(string Status, string? Note);

public sealed record LinkEvidenceRequest(
    Guid ElementId, string SourceType, Guid SourceId, string SourceRef, string? Description);

public sealed record StandardElementDto(
    Guid Id, string ChapterCode, string ChapterTitle, string StandardCode, string ElementCode,
    string Text, int Weight, string ComplianceStatus, string? AssessmentNote,
    Guid? AssessedBy, DateTimeOffset? AssessedAtUtc, int EvidenceCount);

public sealed record StandardSetListItemDto(
    Guid Id, string Framework, string Name, string Version, string Status,
    int ElementCount, decimal CompliancePercent);

public sealed record StandardSetDetailDto(
    Guid Id, string Framework, string Name, string Version, string Status,
    IReadOnlyList<StandardElementDto> Elements);

public sealed record EvidenceLinkDto(
    Guid Id, Guid ElementId, string SourceType, Guid SourceId, string SourceRef,
    string? Description, Guid LinkedBy, DateTimeOffset LinkedAtUtc);

// ── Readiness & gap analysis ─────────────────────────────────────────────────

public sealed record ReadinessScoreDto(
    string ChapterCode, string ChapterTitle,
    int ElementCount, int ApplicableCount, int CompliantCount, int PartialCount,
    int NonCompliantCount, int NotAssessedCount, int NotApplicableCount,
    decimal CompliancePercent);

public sealed record ReadinessDashboardDto(
    Guid StandardSetId, string Framework, string Name, string Version, string Status,
    ReadinessScoreDto Overall, IReadOnlyList<ReadinessScoreDto> Chapters);

/// <summary>A measurable element needing attention, ranked so the biggest wins surface first.</summary>
public sealed record GapItemDto(
    Guid ElementId, string ChapterCode, string StandardCode, string ElementCode, string Text,
    int Weight, string ComplianceStatus, int EvidenceCount, string Reason);
