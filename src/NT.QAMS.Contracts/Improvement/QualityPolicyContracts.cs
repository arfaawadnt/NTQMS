namespace NT.QAMS.Contracts.Improvement;

// ── Quality Policy (controlled) — ISO 9001 §5.2 / ISO 17025 §8.2 ─────────────

public sealed record DraftQualityPolicyRequest(string Statement);

public sealed record ReviseQualityPolicyRequest(string Statement);

public sealed record ApproveQualityPolicyRequest(DateOnly EffectiveDate);

public sealed record QualityPolicyDto(
    Guid Id, string PolicyRef, int Version, string Statement, string Status,
    DateOnly? EffectiveDate, Guid? ApprovedById, DateTimeOffset? ApprovedAtUtc);
