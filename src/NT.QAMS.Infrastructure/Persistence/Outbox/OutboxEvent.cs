namespace NT.QAMS.Infrastructure.Persistence.Outbox;

/// <summary>
/// Transactional outbox row: a serialized domain event written in the SAME
/// transaction as the aggregate change, dispatched afterwards by the
/// OutboxProcessor. At-least-once delivery; consumers are idempotent by EventId.
/// Processed rows are purged by the retention job.
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
}
