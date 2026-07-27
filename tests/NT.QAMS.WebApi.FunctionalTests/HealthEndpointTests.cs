using System.Net;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-0 finding OPS-008 over the real HTTP pipeline: liveness must stay
/// green while the database is down (the factory's PostgreSQL connection
/// string is an unreachable placeholder by design), and readiness must fail —
/// so an orchestrator restarts nothing but routes no traffic. All three
/// endpoints are anonymous (probes carry no credentials) despite the
/// deny-by-default authorization fallback.
/// </summary>
public sealed class HealthEndpointTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_is_healthy_even_with_the_database_down()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Legacy_health_alias_still_answers_as_liveness()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "existing probes (web.config rewrite, verify-e2e.ps1) still target /health");
    }

    [Fact]
    public async Task Readiness_returns_service_unavailable_when_the_database_is_down()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            "a service that cannot reach PostgreSQL must not be routed traffic");
        (await response.Content.ReadAsStringAsync()).Should().Be("Unhealthy");
    }
}
