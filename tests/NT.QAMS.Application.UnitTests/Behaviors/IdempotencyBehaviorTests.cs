using FluentAssertions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Behaviors;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Behaviors;

/// <summary>
/// Phase-4 finding CQRS-004: with an Idempotency-Key present, a retried
/// command replays the FIRST execution's response instead of executing again;
/// without a key (or for queries) the behavior stays out of the way.
/// </summary>
public class IdempotencyBehaviorTests
{
    private sealed record CreateThing : ICommand<Guid>;
    private sealed record ReadThing : IQuery<Guid>;

    private sealed class FixedKey(string? key) : IIdempotencyKeyAccessor
    {
        public string? Key => key;
    }

    private sealed class InMemoryStore : IIdempotencyStore
    {
        private readonly Dictionary<string, string> _rows = [];

        public Task<string?> TryGetResponseAsync(Guid actorId, string key, string requestType, CancellationToken ct) =>
            Task.FromResult(_rows.TryGetValue($"{actorId}|{key}|{requestType}", out var json) ? json : null);

        public Task SaveResponseAsync(Guid actorId, string key, string requestType, string responseJson, CancellationToken ct)
        {
            _rows[$"{actorId}|{key}|{requestType}"] = responseJson;
            return Task.CompletedTask;
        }
    }

    private static (IdempotencyBehavior<CreateThing, Guid> Behavior, InMemoryStore Store, FakeCurrentUser User)
        Harness(string? key)
    {
        var store = new InMemoryStore();
        var user = new FakeCurrentUser();
        return (new IdempotencyBehavior<CreateThing, Guid>(new FixedKey(key), store, user), store, user);
    }

    [Fact]
    public async Task A_retry_with_the_same_key_replays_the_first_response_without_executing()
    {
        var (behavior, _, _) = Harness("retry-1");
        var executions = 0;
        Task<Guid> Handler()
        {
            executions++;
            return Task.FromResult(Guid.CreateVersion7());
        }

        var first = await behavior.Handle(new CreateThing(), Handler, CancellationToken.None);
        var second = await behavior.Handle(new CreateThing(), Handler, CancellationToken.None);

        executions.Should().Be(1, "the retry must not create a second thing");
        second.Should().Be(first, "the caller sees the original result");
    }

    [Fact]
    public async Task Different_keys_execute_independently()
    {
        var store = new InMemoryStore();
        var user = new FakeCurrentUser();
        var executions = 0;
        Task<Guid> Handler()
        {
            executions++;
            return Task.FromResult(Guid.CreateVersion7());
        }

        await new IdempotencyBehavior<CreateThing, Guid>(new FixedKey("a"), store, user)
            .Handle(new CreateThing(), Handler, CancellationToken.None);
        await new IdempotencyBehavior<CreateThing, Guid>(new FixedKey("b"), store, user)
            .Handle(new CreateThing(), Handler, CancellationToken.None);

        executions.Should().Be(2);
    }

    [Fact]
    public async Task Without_a_key_every_call_executes()
    {
        var (behavior, _, _) = Harness(key: null);
        var executions = 0;
        Task<Guid> Handler()
        {
            executions++;
            return Task.FromResult(Guid.CreateVersion7());
        }

        await behavior.Handle(new CreateThing(), Handler, CancellationToken.None);
        await behavior.Handle(new CreateThing(), Handler, CancellationToken.None);

        executions.Should().Be(2, "replay protection is strictly opt-in per request");
    }

    [Fact]
    public async Task Queries_are_never_intercepted()
    {
        var behavior = new IdempotencyBehavior<ReadThing, Guid>(
            new FixedKey("q-1"), new InMemoryStore(), new FakeCurrentUser());
        var executions = 0;
        Task<Guid> Handler()
        {
            executions++;
            return Task.FromResult(Guid.CreateVersion7());
        }

        await behavior.Handle(new ReadThing(), Handler, CancellationToken.None);
        await behavior.Handle(new ReadThing(), Handler, CancellationToken.None);

        executions.Should().Be(2, "reads are naturally idempotent — no storage cost");
    }
}
