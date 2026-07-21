namespace NT.QAMS.SharedKernel.Primitives;

/// <summary>
/// Base class for all entities. Identity equality; Guid v7 ids are generated
/// app-side so ids exist before commit (outbox/event use).
/// </summary>
public abstract class Entity
{
    protected Entity(Guid id)
    {
        Id = id;
    }

    protected Entity()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid Id { get; protected set; }

    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
