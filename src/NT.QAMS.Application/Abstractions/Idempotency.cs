namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// CQRS-004: the caller's idempotency key for the current unit of work — the
/// WebApi supplies it from the <c>Idempotency-Key</c> request header; null in
/// background scopes and when the client sent none.
/// </summary>
public interface IIdempotencyKeyAccessor
{
    string? Key { get; }
}

/// <summary>
/// Persistence port for replayed command responses, keyed per actor so one
/// client's key can never replay another's response.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>The stored response for (actor, key, request type), or null on first sight.</summary>
    Task<string?> TryGetResponseAsync(
        Guid actorId, string key, string requestType, CancellationToken cancellationToken);

    /// <summary>Records the executed command's response for replay.</summary>
    Task SaveResponseAsync(
        Guid actorId, string key, string requestType, string responseJson, CancellationToken cancellationToken);
}
