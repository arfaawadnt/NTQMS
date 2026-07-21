namespace NT.QAMS.SharedKernel.Primitives;

/// <summary>
/// A domain event: a past-tense fact raised by an aggregate, persisted to the
/// transactional outbox in the same transaction as the state change, then
/// dispatched to in-process policy handlers, projections, and ledgers.
/// Implementations must be JSON-serializable records carrying refs, not object graphs.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}
