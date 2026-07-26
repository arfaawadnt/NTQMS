namespace NT.QAMS.Contracts.IdentityAccess;

// ── Periodic user-access review (F-11 / Part 11 §11.10(d) / Annex 11 §12) ─────

public sealed record CompleteAccessReviewRequest(bool ChangesRequired, string Conclusion);

public sealed record UserAccessReviewDto(
    Guid Id, string ReviewRef, DateOnly OpenedOn, string Status,
    Guid? ReviewedBy, DateTimeOffset? CompletedAtUtc,
    int? AccountsReviewed, bool? ChangesRequired, string? Conclusion);
