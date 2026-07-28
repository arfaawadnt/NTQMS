using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NT.QAMS.WebApi.Middleware;
using NT.QAMS.WebApi.Security;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-3 findings SEC-011/012/013 over the real HTTP pipeline: the
/// defensive header set (including the inline-script-blocking CSP and HSTS)
/// rides EVERY response — success and error alike — and a burst on the
/// credential surface is rejected with 429 + Retry-After.
/// </summary>
public sealed class SecurityHardeningTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]   // anonymous probe
    [InlineData("/api/tenants")]   // deny-by-default 401 — headers still ride
    public async Task Every_response_carries_the_defensive_header_set(string path)
    {
        var response = await _client.GetAsync(path);

        var headers = response.Headers;
        headers.GetValues("Content-Security-Policy").Single()
            .Should().Be(SecurityHeadersMiddleware.ApiContentSecurityPolicy,
                "default-src 'none' grants no script source at all — inline script is blocked by definition");
        headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        headers.GetValues("X-Frame-Options").Single().Should().Be("DENY");
        headers.GetValues("Referrer-Policy").Single().Should().Be("no-referrer");
        // The factory boots the Production environment, so the SEC-012 HSTS
        // commitment must be present.
        headers.GetValues("Strict-Transport-Security").Single()
            .Should().Be(SecurityHeadersMiddleware.StrictTransportSecurityValue);
    }

    [Fact]
    public async Task A_burst_of_login_attempts_is_rejected_with_429()
    {
        // Tighten only THIS host's auth budget; the shared factory keeps the
        // generous limits the rest of the suite relies on.
        using var throttled = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(
                new RateLimitSettings(100000, AuthPermitPerMinute: 3, 100000, 100000))));
        var client = throttled.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { email = "attacker@nowhere.test", password = $"guess-{attempt}" });
            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                response.Headers.RetryAfter.Should().NotBeNull(
                    "a throttled client is told when to come back");
            }
        }

        statuses.Take(3).Should().NotContain(HttpStatusCode.TooManyRequests,
            "legitimate first attempts pass");
        statuses.Skip(3).Should().Contain(HttpStatusCode.TooManyRequests,
            "SEC-013: a credential-guessing burst must be throttled");
    }

    [Fact]
    public async Task Health_probes_are_never_throttled()
    {
        using var throttled = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(
                new RateLimitSettings(GlobalPermitPerMinute: 2, 100000, 100000, 100000))));
        var client = throttled.CreateClient();

        for (var i = 0; i < 10; i++)
        {
            (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK,
                "probes and scrapers sit outside the limiter — throttling them breaks monitoring");
        }
    }
}
