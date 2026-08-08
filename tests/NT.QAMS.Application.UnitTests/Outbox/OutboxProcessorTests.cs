using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Application.Notifications;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Abstractions;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Outbox;

/// <summary>
/// Phase-1 findings MSG-004/005/006 on the real processor + DbContext +
/// MediatR pipeline: a poison event retries on a backoff schedule and
/// dead-letters after MaxAttempts without ever blocking a healthy event
/// behind it, and redelivery after a crash-before-mark nets exactly one
/// side-effect (the policies' natural-key idempotency).
/// </summary>
public class OutboxProcessorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    /// <summary>Advances so backoff schedules can be stepped through.</summary>
    private sealed class SteppingClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = T0;
    }

    private sealed class NoopEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static (ServiceProvider Provider, OutboxProcessor Processor, SteppingClock Clock) BuildHarness(string dbName)
    {
        var clock = new SteppingClock();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(clock);
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentTenantSetter>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddScoped<IReferenceNumberGenerator, FakeRefGenerator>();
        services.AddScoped<NotificationDispatcher>();
        services.AddSingleton<IEmailSender, NoopEmailSender>();
        services.AddScoped<AuditStampInterceptor>();
        services.AddScoped<TenantStampInterceptor>();
        services.AddScoped<OutboxInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(
                sp.GetRequiredService<AuditStampInterceptor>(),
                sp.GetRequiredService<TenantStampInterceptor>(),
                sp.GetRequiredService<OutboxInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PtToNcPolicy).Assembly));
        var provider = services.BuildServiceProvider();

        var processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            new OutboxOptions(30),
            NullLogger<OutboxProcessor>.Instance);
        return (provider, processor, clock);
    }

    private static OutboxEvent PoisonRow(DateTimeOffset occurredAt) => new()
    {
        Id = Guid.CreateVersion7(),
        EventType = "Ghost.Event, Nowhere", // unresolvable type — permanently failing
        Payload = "{}",
        OccurredAtUtc = occurredAt,
    };

    private static OutboxEvent HealthyRow(DateTimeOffset occurredAt)
    {
        var evt = new PtUnsatisfactory(
            Guid.CreateVersion7(), "PT-2026-0001", "Glucose", 3.4m, TenantId, Guid.CreateVersion7());
        return new OutboxEvent
        {
            Id = Guid.CreateVersion7(),
            TenantId = TenantId,
            EventType = $"{typeof(PtUnsatisfactory).FullName}, {typeof(PtUnsatisfactory).Assembly.GetName().Name}",
            Payload = JsonSerializer.Serialize(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            OccurredAtUtc = occurredAt,
        };
    }

    private static async Task SeedAsync(ServiceProvider provider, params OutboxEvent[] rows)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<OutboxEvent>().AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task<OutboxEvent> RowAsync(ServiceProvider provider, Guid id)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<OutboxEvent>().AsNoTracking().SingleAsync(e => e.Id == id);
    }

    [Fact]
    public async Task Poison_event_dead_letters_after_max_attempts_and_never_blocks_a_healthy_one()
    {
        var (provider, processor, clock) = BuildHarness($"outbox-poison-{Guid.NewGuid()}");
        var poison = PoisonRow(T0);              // at the head of the stream
        var healthy = HealthyRow(T0.AddSeconds(1)); // behind the poison event
        await SeedAsync(provider, poison, healthy);
        clock.UtcNow = T0.AddSeconds(5);

        // First pass: the healthy event processes even though the poison event
        // ahead of it fails — no head-of-line blocking.
        await processor.ProcessBatchAsync(CancellationToken.None);

        (await RowAsync(provider, healthy.Id)).ProcessedAtUtc.Should().NotBeNull(
            "a failing event must not block the healthy event behind it");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Nonconformances.IgnoreQueryFilters().CountAsync(n => n.SourceRef == "PT:PT-2026-0001"))
                .Should().Be(1, "the healthy event's policy ran");
        }

        var afterFirst = await RowAsync(provider, poison.Id);
        afterFirst.Attempts.Should().Be(1);
        afterFirst.LastError.Should().Contain("Ghost.Event");
        afterFirst.NextAttemptAtUtc.Should().BeAfter(clock.UtcNow, "MSG-005: retries follow a backoff schedule");

        // Drain the follow-on events the policy itself raised (NcRaised → outbox).
        // While the backoff is pending the poison row is never claimed again.
        while (await processor.ProcessBatchAsync(CancellationToken.None) > 0)
        {
        }

        (await RowAsync(provider, poison.Id)).Attempts.Should().Be(1,
            "the poison event is not due until its backoff elapses");

        // Step through the remaining attempts by advancing past each backoff.
        for (var attempt = 2; attempt <= OutboxProcessor.MaxAttempts; attempt++)
        {
            var pending = await RowAsync(provider, poison.Id);
            clock.UtcNow = pending.NextAttemptAtUtc!.Value.AddSeconds(1);
            await processor.ProcessBatchAsync(CancellationToken.None);
        }

        var deadLettered = await RowAsync(provider, poison.Id);
        deadLettered.Attempts.Should().Be(OutboxProcessor.MaxAttempts);
        deadLettered.DeadLetteredAtUtc.Should().NotBeNull("MSG-004: exhausted events leave the retry stream");
        deadLettered.ProcessedAtUtc.Should().BeNull();

        // Dead-lettered rows are out of the stream for good.
        clock.UtcNow += TimeSpan.FromHours(1);
        (await processor.ProcessBatchAsync(CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task Redelivery_after_crash_before_mark_nets_a_single_side_effect()
    {
        var (provider, processor, clock) = BuildHarness($"outbox-redelivery-{Guid.NewGuid()}");
        var healthy = HealthyRow(T0);
        await SeedAsync(provider, healthy);
        clock.UtcNow = T0.AddSeconds(5);

        await processor.ProcessBatchAsync(CancellationToken.None);

        // Simulate the at-least-once crash window: published, but the mark was lost.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Set<OutboxEvent>().SingleAsync(e => e.Id == healthy.Id);
            row.ProcessedAtUtc = null;
            await db.SaveChangesAsync();
        }

        await processor.ProcessBatchAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Nonconformances.IgnoreQueryFilters().CountAsync(n => n.SourceRef == "PT:PT-2026-0001"))
                .Should().Be(1, "MSG-006: redelivery must net exactly one side-effect");
        }

        (await RowAsync(provider, healthy.Id)).ProcessedAtUtc.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Backoff_is_exponential_with_bounded_jitter(int attempts)
    {
        var floor = OutboxProcessor.BackoffBase * Math.Pow(2, attempts - 1);
        var ceiling = floor * 1.25;

        for (var i = 0; i < 50; i++)
        {
            var backoff = OutboxProcessor.ComputeBackoff(attempts);
            backoff.Should().BeGreaterThanOrEqualTo(floor);
            backoff.Should().BeLessThanOrEqualTo(ceiling);
        }
    }
}
