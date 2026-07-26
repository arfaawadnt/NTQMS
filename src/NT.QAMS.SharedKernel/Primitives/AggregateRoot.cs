namespace NT.QAMS.SharedKernel.Primitives;

/// <summary>
/// Base class for aggregate roots. Collects domain events for the outbox
/// interceptor to drain in the same transaction as the state change.
/// </summary>
public abstract class AggregateRoot : Entity, IAuditable
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Segregation of duties (Part 11 §11.10(g)): the person who prepared/created
    /// this record cannot also sign it off. No-op when the preparer is unknown
    /// (records created before the id was captured, or by background/system work).
    /// </summary>
    protected void EnsureSignerIsNotPreparer(Guid signerId, string code)
    {
        if (CreatedByUserId is { } preparer && preparer == signerId)
        {
            throw new DomainException(code, "Segregation of duties: the preparer of a record cannot sign it off.");
        }
    }
}
