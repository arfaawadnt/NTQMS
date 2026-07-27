using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Persistence.Idempotency;

/// <summary>
/// CQRS-004: one executed command's stored response, replayed when the same
/// actor retries the same Idempotency-Key. Short-lived transport state — the
/// retention purge removes rows after <see cref="Retention"/>.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>Replay window: retries arrive within seconds; a day is generous.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public Guid Id { get; init; }
    public Guid ActorId { get; init; }
    public string IdempotencyKey { get; init; } = null!;
    public string RequestType { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public DateTimeOffset CreatedAtUtc { get; init; }
}

/// <summary>EF-backed store; the unique (actor, key, type) index is the replay anchor.</summary>
public sealed class EfIdempotencyStore(AppDbContext db, IClock clock) : IIdempotencyStore
{
    public Task<string?> TryGetResponseAsync(
        Guid actorId, string key, string requestType, CancellationToken cancellationToken) =>
        db.Set<IdempotencyRecord>().AsNoTracking()
            .Where(r => r.ActorId == actorId && r.IdempotencyKey == key && r.RequestType == requestType)
            .Select(r => (string?)r.ResponseJson)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveResponseAsync(
        Guid actorId, string key, string requestType, string responseJson, CancellationToken cancellationToken)
    {
        db.Set<IdempotencyRecord>().Add(new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            ActorId = actorId,
            IdempotencyKey = key,
            RequestType = requestType,
            ResponseJson = responseJson,
            CreatedAtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Background scopes have no HTTP request and therefore no key.</summary>
public sealed class NullIdempotencyKeyAccessor : IIdempotencyKeyAccessor
{
    public string? Key => null;
}
