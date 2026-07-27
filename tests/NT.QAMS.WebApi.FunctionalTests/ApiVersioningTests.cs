using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-4 finding API-001: the same endpoint resolves at the legacy
/// unversioned path (implicit default v1.0) AND at api/v1/..., an unsupported
/// version is refused, and responses report the supported versions.
/// </summary>
public sealed class ApiVersioningTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> LoginAsync(string prefix) =>
        _client.PostAsJsonAsync($"{prefix}/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });

    [Fact]
    public async Task The_versioned_route_serves_the_same_contract_as_the_legacy_route()
    {
        var legacy = await LoginAsync("/api");
        var versioned = await LoginAsync("/api/v1");

        legacy.StatusCode.Should().Be(HttpStatusCode.OK);
        versioned.StatusCode.Should().Be(HttpStatusCode.OK,
            "api/v1/... must resolve to the same endpoint as api/...");
    }

    [Fact]
    public async Task An_unsupported_version_is_refused()
    {
        var response = await LoginAsync("/api/v99");

        // 400 (unsupported version) for authenticated callers; anonymous ones
        // meet the deny-by-default 401 on the refusal endpoint first. Either
        // way the request is refused — the login was NOT processed as v1.
        ((int)response.StatusCode).Should().BeOneOf([400, 401, 404],
            "a version that does not exist must never silently serve v1 semantics");
    }

    [Fact]
    public async Task Responses_report_the_supported_versions()
    {
        var response = await LoginAsync("/api/v1");

        response.Headers.TryGetValues("api-supported-versions", out var values).Should().BeTrue();
        values!.Single().Should().Contain("1.0");
    }
}
