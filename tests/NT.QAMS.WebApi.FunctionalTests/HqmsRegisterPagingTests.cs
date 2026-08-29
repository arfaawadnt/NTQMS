using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Audit finding M-10: the register-scale clinical endpoints (patient-safety
/// events, HAI cases, device exposures) must page — a hospital tenant's
/// lifetime of events cannot travel as one bare array. Pinned to the house
/// envelope (items/total/page/pageSize).
/// </summary>
public sealed class HqmsRegisterPagingTests(QamsWebAppFactory factory)
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

        var slug = $"page-lab-{Guid.NewGuid():N}"[..20];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Paging Lab",
            adminEmail = $"qa@{slug}.test",
            adminDisplayName = "QA",
            adminPassword = "Page-Lab-Pass-1!",
        })).EnsureSuccessStatusCode();

        var tenantLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"qa@{slug}.test",
            password = "Page-Lab-Pass-1!",
        });
        var tenantAdmin = factory.CreateClient();
        tenantAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return tenantAdmin;
    }

    [Theory]
    [InlineData("/api/patient-safety/events")]
    [InlineData("/api/infection-control/cases")]
    [InlineData("/api/infection-control/devices")]
    public async Task Register_scale_clinical_endpoints_return_the_paging_envelope(string path)
    {
        var admin = await TenantAdminClientAsync();

        var response = await admin.GetAsync($"{path}?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.ValueKind.Should().Be(JsonValueKind.Object,
            $"M-10: '{path}' must return the paging envelope, not a bare array");
        body.RootElement.TryGetProperty("items", out _).Should().BeTrue();
        body.RootElement.TryGetProperty("total", out _).Should().BeTrue();
        body.RootElement.GetProperty("pageSize").GetInt32().Should().Be(10);
    }
}
