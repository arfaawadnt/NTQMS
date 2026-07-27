using System.Text.Json;
using MediatR;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.Application.Behaviors;

/// <summary>
/// CQRS-004: retry-safe unsafe commands. When the caller supplies an
/// <c>Idempotency-Key</c>, the first execution's response is stored per
/// (actor, key, command type); a RETRY with the same key replays that stored
/// response instead of executing again — a network timeout can no longer net
/// two nonconformances. Scope guards: commands only, authenticated actors
/// only, and a key is required (no key ⇒ normal execution). This protects
/// sequential retries; concurrent duplicates remain covered by the natural
/// unique keys (e.g. ux_nonconformance_source, tenant-scoped refs).
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyKeyAccessor keyAccessor,
    IIdempotencyStore store,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly bool IsCommand =
        typeof(TRequest).GetInterfaces().Any(i =>
            i == typeof(ICommand) ||
            (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand || keyAccessor.Key is not { } key || currentUser.UserId is not { } actorId)
        {
            return await next();
        }

        var requestType = typeof(TRequest).FullName!;
        var replayed = await store.TryGetResponseAsync(actorId, key, requestType, cancellationToken);
        if (replayed is not null)
        {
            return JsonSerializer.Deserialize<TResponse>(replayed, SerializerOptions)!;
        }

        var response = await next();
        await store.SaveResponseAsync(
            actorId, key, requestType,
            JsonSerializer.Serialize(response, SerializerOptions), cancellationToken);
        return response;
    }
}
