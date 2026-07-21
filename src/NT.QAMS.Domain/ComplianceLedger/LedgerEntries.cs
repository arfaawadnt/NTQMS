namespace NT.QAMS.Domain.ComplianceLedger;

/// <summary>
/// A tamper-evident audit-trail entry. Append-only and hash-chained per tenant:
/// EntryHash = SHA-256(PrevHash ‖ Sequence ‖ EventId ‖ EventType ‖ Payload ‖ OccurredAt).
/// A break anywhere in the chain is detectable (21 CFR Part 11 §11.10(e)).
/// Plain persistence type — never mutated after insert; the DB grants no UPDATE/DELETE.
/// </summary>
public sealed class AuditTrailEntry
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public long Sequence { get; init; }
    public Guid EventId { get; init; }
    public string EventType { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string PrevHash { get; init; } = null!;
    public string EntryHash { get; init; } = null!;
}

/// <summary>
/// The durable record of an electronic signature (21 CFR Part 11 §11.50/§11.70):
/// who signed, what it meant, what was signed, and a content hash linking the
/// signature to the exact signed payload. Append-only.
/// </summary>
public sealed class SignatureRecord
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid SignerId { get; init; }
    public string SignerDisplay { get; init; } = null!;
    public string Meaning { get; init; } = null!;
    public string SubjectRef { get; init; } = null!;
    public string ContentHash { get; init; } = null!;
    public DateTimeOffset SignedAtUtc { get; init; }
}

/// <summary>
/// Security-relevant events (Part 11 §11.10(d) / ISO 17025 7.11): logins,
/// lockouts, MFA challenges, session/privilege changes. Append-only.
/// </summary>
public sealed class SecurityEvent
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string EventType { get; init; } = null!;
    public string? Actor { get; init; }
    public string? IpAddress { get; init; }
    public string? Detail { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
}
