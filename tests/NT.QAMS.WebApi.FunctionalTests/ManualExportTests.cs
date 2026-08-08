using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// URS-131 over the real pipeline: the User Manual export renders a genuine PDF
/// from caller-supplied (SPA-localized) content, refuses an unauthenticated caller,
/// and rejects an empty payload with EXPORT-003 before rendering.
/// </summary>
public sealed class ManualExportTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);

    private async Task<HttpClient> TenantAdminClientAsync()
    {
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);

        var slug = $"manual-{Guid.NewGuid():N}"[..18];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Manual Lab",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "Tenant Admin",
            adminPassword = "Manual-Admin-1!",
        })).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"admin@{slug}.test",
            password = "Manual-Admin-1!",
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return client;
    }

    private static object SamplePayload() => new
    {
        language = "en",
        groups = new[]
        {
            new
            {
                title = "Overview",
                topics = new[]
                {
                    new
                    {
                        route = "/nonconformances",
                        title = "NC & CAPA",
                        summary = "Raise and work nonconformances.",
                        steps = new[]
                        {
                            new { label = "Raise", detail = "Capture the event." },
                            new { label = "Verify", detail = "Sign off the corrective action." },
                        },
                        usage = new[] { "Click Raise NC.", "Verify effectiveness." },
                    },
                },
            },
        },
    };

    [Fact]
    public async Task Manual_endpoint_returns_a_genuine_pdf_to_an_authenticated_caller()
    {
        var client = await TenantAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/exports/manual.pdf", SamplePayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Empty_manual_payload_is_rejected_before_rendering()
    {
        var client = await TenantAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/exports/manual.pdf",
            new { language = "en", groups = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("EXPORT-003");
    }

    [Fact]
    public async Task Unauthenticated_caller_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/exports/manual.pdf", SamplePayload());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
