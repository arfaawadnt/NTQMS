namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// Classifies provider-specific persistence failures for the few handlers that
/// must REACT to them rather than fail (M-12: the ADT inbox turns a concurrent
/// duplicate delivery into the idempotent result instead of a 500). Keeps the
/// provider exception types out of the application layer.
/// </summary>
public interface IDatabaseErrorClassifier
{
    /// <summary>True when the exception is a unique-constraint violation (e.g. PostgreSQL 23505).</summary>
    bool IsUniqueViolation(Exception exception);
}
