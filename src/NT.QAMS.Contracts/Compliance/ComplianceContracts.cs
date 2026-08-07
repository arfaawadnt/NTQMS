namespace NT.QAMS.Contracts.Compliance;

// ── Periodic Audit-Trail Review (21 CFR Part 11 §11.10(e)) ──────────────────

public sealed record OpenAuditTrailReviewRequest(DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record CompleteAuditTrailReviewRequest(bool AnomaliesFound, string Conclusion, string Password, string Pin);

public sealed record AuditTrailReviewDto(
    Guid Id, string ReviewRef, DateOnly PeriodStart, DateOnly PeriodEnd, string Status,
    Guid? ReviewedBy, DateTimeOffset? CompletedAtUtc, int? EventsReviewed, int? FieldChangesReviewed,
    bool? AnomaliesFound, string? Conclusion);
