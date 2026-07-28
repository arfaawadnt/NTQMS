using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-9 finding (ARCH-005 depth): the response CONTRACT holds uniformly
/// across modules, not just on the sampled endpoints of earlier tests. Every
/// list endpoint answers with the API-004 pagination envelope on both the
/// legacy and the api/v1 route; every by-id read of a missing resource answers
/// with a problem+json 404 carrying a stable code.
/// </summary>
public sealed class ContractCoverageTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);

    // One list endpoint per module that returns the pagination envelope.
    private static readonly string[] ListEndpoints =
    [
        "/api/nonconformances",
        "/api/documents",
        "/api/audits",
        "/api/equipment",
        "/api/competencies",
        "/api/training-assignments",
        "/api/risks",
        "/api/changes",
        "/api/management-reviews",
        "/api/suppliers",
        "/api/archives",
        "/api/tasks/mine",
        "/api/notifications/mine",
    ];

    private static readonly string MissingId = Guid.NewGuid().ToString();
    private static readonly string[] ByIdEndpoints =
    [
        "/api/nonconformances/" + MissingId,
        "/api/documents/" + MissingId,
        "/api/audits/" + MissingId,
        "/api/equipment/" + MissingId,
        "/api/risks/" + MissingId,
    ];

    private async Task<HttpClient> TenantClientAsync()
    {
        // The functional host provisions no seed tenant; use platform admin to
        // create one, then sign in as its admin.
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);

        var slug = $"contract-{Guid.NewGuid():N}"[..18];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Contract Lab",
            adminEmail = $"qa@{slug}.test",
            adminDisplayName = "QA",
            adminPassword = "Contract-Pass-1!",
        })).EnsureSuccessStatusCode();

        var tenantLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"qa@{slug}.test",
            password = "Contract-Pass-1!",
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return client;
    }

    [Fact]
    public async Task Every_list_endpoint_returns_the_pagination_envelope_on_legacy_and_v1()
    {
        var client = await TenantClientAsync();

        foreach (var path in ListEndpoints)
        {
            foreach (var route in new[] { path, path.Replace("/api/", "/api/v1/") })
            {
                var response = await client.GetAsync(route);
                response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route} should list");

                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                foreach (var field in new[] { "items", "total", "page", "pageSize", "hasMore" })
                {
                    body.TryGetProperty(field, out _).Should().BeTrue(
                        $"{route}: the pagination envelope must carry '{field}'");
                }

                body.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array, $"{route}: items is an array");
            }
        }
    }

    [Fact]
    public async Task Every_by_id_read_of_a_missing_resource_is_problem_json_404()
    {
        var client = await TenantClientAsync();

        foreach (var path in ByIdEndpoints)
        {
            var response = await client.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{path} should be a not-found");
            response.Content.Headers.ContentType!.MediaType.Should()
                .Be("application/problem+json", $"{path}: not-found uses the uniform contract");

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("code").GetString().Should()
                .EndWith("-404", $"{path}: a stable not-found code");
            body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }
}
