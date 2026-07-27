namespace NT.QAMS.Infrastructure.Persistence.Outbox;

/// <summary>
/// Transactional outbox row: a serialized domain event written in the SAME
/// transaction as the aggregate change, dispatched afterwards by the
/// OutboxProcessor. At-least-once delivery; consumers are idempotent by
/// natural key (see the *Policy handlers) or by EventId (notifications).
/// Failed rows retry on an exponential-backoff schedule
/// (<see cref="NextAttemptAtUtc"/>) and dead-letter after MaxAttempts
/// (<see cref="DeadLetteredAtUtc"/>) so one poison event can never block the
/// stream. Rows are claimed under a lease (<see cref="ClaimedUntilUtc"/> via
/// FOR UPDATE SKIP LOCKED) so concurrent processors each publish a row once.
/// Processed rows are purged after the retention window (Outbox:RetentionDays).
/// </summary>
public sealed class OutboxEvent
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string EventType { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTimeOffset OccurredAtUtc { get; init; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    /// <summary>Earliest moment the next delivery attempt may run (MSG-005); null = due now.</summary>
    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    /// <summary>Set when the row exhausted MaxAttempts and left the retry stream (MSG-004).</summary>
    public DateTimeOffset? DeadLetteredAtUtc { get; set; }

    /// <summary>Processing lease (OPS-002): the row belongs to one claimant until this expires.</summary>
    public DateTimeOffset? ClaimedUntilUtc { get; set; }

    /// <summary>
    /// W3C traceparent of the operation that wrote the row (OBS-002) — the
    /// processor parents the delivery span on it, so one trace spans the
    /// HTTP request and the async outbox work.
    /// </summary>
    public string? TraceParent { get; init; }
}
