using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NT.QAMS.WebApi.Middleware;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-2 findings OBS-001/002/003 over the real HTTP pipeline:
/// the correlation id is echoed (caller-supplied or generated), error bodies
/// carry traceId + correlationId, the /metrics endpoint answers anonymously,
/// and the canonical request-completion log carries every required field —
/// the log-shape contract.
/// </summary>
public sealed class ObservabilityTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_caller_supplied_correlation_id_is_echoed_on_the_response()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(ObservabilityMiddleware.CorrelationHeaderName, "ticket-4711");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues(ObservabilityMiddleware.CorrelationHeaderName)
            .Should().ContainSingle().Which.Should().Be("ticket-4711");
    }

    [Fact]
    public async Task A_correlation_id_is_generated_when_none_is_supplied()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.GetValues(ObservabilityMiddleware.CorrelationHeaderName)
            .Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_malicious_correlation_id_is_replaced_not_echoed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(
            ObservabilityMiddleware.CorrelationHeaderName, "abc<script>alert(1)</script>");

        var response = await _client.SendAsync(request);

        var echoed = response.Headers.GetValues(ObservabilityMiddleware.CorrelationHeaderName).Single();
        echoed.Should().NotContain("<", "unsafe input must never be reflected");
    }

    [Fact]
    public async Task Error_problem_details_carry_trace_and_correlation_ids()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "", password = "" }),
        };
        request.Headers.Add(ObservabilityMiddleware.CorrelationHeaderName, "err-corr-1");

        var response = await _client.SendAsync(request);

        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("correlationId").GetString().Should().Be("err-corr-1");
    }

    [Fact]
    public async Task Metrics_endpoint_answers_anonymously_in_prometheus_format()
    {
        var response = await _client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "scrapers carry no credentials");
        response.Content.Headers.ContentType!.MediaType.Should().StartWith("text/plain");
    }

    [Fact]
    public async Task The_request_completion_log_carries_every_required_field()
    {
        var records = new ConcurrentQueue<IReadOnlyList<KeyValuePair<string, object?>>>();
        using var observed = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(new StateCapturingLoggerProvider(records));
            logging.AddFilter(typeof(ObservabilityMiddleware).FullName, LogLevel.Information);
        }));
        var client = observed.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(ObservabilityMiddleware.CorrelationHeaderName, "log-shape-1");
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);

        var completion = records.FirstOrDefault(state =>
            state.Any(pair => pair.Key == "CorrelationId" && (string?)pair.Value == "log-shape-1"));
        completion.Should().NotBeNull("every request must emit one completion record");

        var keys = completion!.Select(pair => pair.Key).ToList();
        // OBS-001: the standard enriched property set, asserted as a contract.
        keys.Should().Contain(["Service", "Environment", "Method", "Path", "Operation",
            "Status", "Outcome", "DurationMs", "TenantId", "UserId", "CorrelationId"]);

        var state = completion!.ToDictionary(pair => pair.Key, pair => pair.Value);
        state["Service"].Should().Be(ObservabilityMiddleware.ServiceName);
        state["Environment"].Should().Be("Production");
        state["Outcome"].Should().Be("success");
        state["Status"].Should().Be(200);
    }

    /// <summary>Captures structured log STATE (not rendered text) for shape assertions.</summary>
    private sealed class StateCapturingLoggerProvider(
        ConcurrentQueue<IReadOnlyList<KeyValuePair<string, object?>>> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new StateLogger(sink);
        public void Dispose() { }

        private sealed class StateLogger(
            ConcurrentQueue<IReadOnlyList<KeyValuePair<string, object?>>> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (state is IReadOnlyList<KeyValuePair<string, object?>> structured)
                {
                    sink.Enqueue(structured);
                }
            }
        }
    }
}
