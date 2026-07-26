namespace NT.QAMS.Contracts.DocumentControl;

public sealed record CreateDocumentRequest(
    string Code, string Title, string Category, Guid FileId, string ChangeSummary,
    int ReviewCycleMonths = 24);

public sealed record DraftNewVersionRequest(Guid FileId, string ChangeSummary, string Bump);

public sealed record RejectVersionRequest(string Reason);

/// <summary>Publishing requires the approver's 4-digit e-signature PIN.</summary>
public sealed record PublishDocumentRequest(string Password, string Pin);

public sealed record DocumentVersionDto(
    Guid Id, string Version, string State, Guid FileId, string ChangeSummary,
    Guid AuthorId, Guid? RecommendedBy, DateTimeOffset? RecommendedAtUtc,
    Guid? ApprovedBy, DateTimeOffset? ApprovedAtUtc, string? RejectionReason);

public sealed record DocumentListItemDto(
    Guid Id, string Code, string Title, string Category, string Status,
    string? PublishedVersion, DateTimeOffset CreatedAtUtc);

public sealed record DocumentDetailDto(
    Guid Id, string Code, string Title, string Category, string Status,
    DateTimeOffset CreatedAtUtc, IReadOnlyList<DocumentVersionDto> Versions,
    int ReviewCycleMonths = 24, DateOnly? NextReviewDue = null);

public sealed record FileUploadedDto(Guid Id, string FileName, string Sha256, long SizeBytes);

// ── Read-and-understand acknowledgements (ISO 9001 §7.5 / 17025 §8.3 / Part 11) ──

/// <summary>Whether the current user has acknowledged the document's current published version.</summary>
public sealed record MyDocumentAcknowledgementDto(
    string? PublishedVersion, bool Acknowledged, DateTimeOffset? AcknowledgedAtUtc);

public sealed record DocumentAcknowledgementDto(
    Guid UserId, string UserDisplay, string VersionLabel, DateTimeOffset AcknowledgedAtUtc);
