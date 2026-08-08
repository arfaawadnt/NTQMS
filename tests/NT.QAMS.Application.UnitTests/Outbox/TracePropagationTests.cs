using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Notifications;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Infrastructure.Observability;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Abstractions;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Outbox;

/// <summary>
/// Phase-2 finding OBS-002, the acceptance test for trace-context propagation
/// across the async job boundary: the outbox interceptor stores the WRITING
/// operation's W3C traceparent on the row, and the processor parents the
/// delivery span on it — so the HTTP request and the asynchronous outbox
/// delivery share one trace id.
/// </summary>
public class TracePropagationTests
{
    private const string TestSourceName = "obs-propagation-test";
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private sealed class NoopEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new FixedClock(Now));
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
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(NotificationDispatcher).Assembly));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Outbox_delivery_span_joins_the_trace_that_wrote_the_row()
    {
        var provider = BuildProvider($"trace-prop-{Guid.NewGuid()}");
        var deliverySpans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name is QamsDiagnostics.OutboxSourceName or TestSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.Source.Name == QamsDiagnostics.OutboxSourceName)
                {
                    deliverySpans.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        using var testSource = new ActivitySource(TestSourceName);
        ActivityTraceId writingTraceId;

        // "The HTTP request": an ambient activity is current while the
        // aggregate change + outbox row are saved.
        using (var writingActivity = testSource.StartActivity("incoming request"))
        {
            writingActivity.Should().NotBeNull();
            writingTraceId = writingActivity!.TraceId;

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Set(TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nc = Nonconformance.Raise(
                "NC-2026-0001", "trace propagation", "details", 3, 3,
                NcSourceType.Internal, Guid.CreateVersion7(), null);
            nc.Submit(); // NcRaised fires on entry to the register
            db.Nonconformances.Add(nc);
            await db.SaveChangesAsync();

            var row = await db.Set<OutboxEvent>().AsNoTracking().SingleAsync();
            row.TraceParent.Should().Be(writingActivity.Id,
                "the interceptor must persist the writing trace across the async boundary");
        }

        // The writing activity is finished — the async boundary is real.
        var processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IClock>(),
            new OutboxOptions(30),
            NullLogger<OutboxProcessor>.Instance);
        await processor.ProcessBatchAsync(CancellationToken.None);

        // NOTE: other test classes may process their own outbox rows in
        // parallel — assert on OUR trace, not on the first captured span.
        deliverySpans.Should().NotBeEmpty("processing the row must produce a delivery span");
        var mine = deliverySpans.SingleOrDefault(span => span.TraceId == writingTraceId);
        mine.Should().NotBeNull("HTTP → EF → Outbox delivery must share one trace id");
        mine!.Source.Name.Should().Be(QamsDiagnostics.OutboxSourceName);
    }
}
