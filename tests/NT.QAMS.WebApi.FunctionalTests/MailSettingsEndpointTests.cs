using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// URS-133 over the real pipeline: the Mail Management endpoints read a
/// not-yet-configured default, persist a tenant's sender identity, and refuse an
/// unauthenticated caller. Gated by <c>notifications.manage</c>.
/// </summary>
public sealed class MailSettingsEndpointTests(QamsWebAppFactory factory)
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

        var slug = $"mail-{Guid.NewGuid():N}"[..18];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Mail Lab",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "Tenant Admin",
            adminPassword = "Mail-Admin-1!",
        })).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"admin@{slug}.test",
            password = "Mail-Admin-1!",
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return client;
    }

    [Fact]
    public async Task Get_returns_a_not_configured_default_then_reflects_a_saved_identity()
    {
        var client = await TenantAdminClientAsync();

        var initial = await client.GetFromJsonAsync<JsonElement>("/api/notifications/mail-settings");
        initial.GetProperty("configured").GetBoolean().Should().BeFalse();
        initial.GetProperty("enabled").GetBoolean().Should().BeTrue("mail defaults to enabled");

        var put = await client.PutAsJsonAsync("/api/notifications/mail-settings", new
        {
            fromName = "Mail Lab Quality",
            fromAddress = "quality@mail-lab.test",
            replyTo = (string?)null,
            enabled = true,
            brandColor = "#00B2A9",
            footerNote = "Confidential — quality record.",
        });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var saved = await client.GetFromJsonAsync<JsonElement>("/api/notifications/mail-settings");
        saved.GetProperty("configured").GetBoolean().Should().BeTrue();
        saved.GetProperty("fromName").GetString().Should().Be("Mail Lab Quality");
        saved.GetProperty("fromAddress").GetString().Should().Be("quality@mail-lab.test");
        saved.GetProperty("brandColor").GetString().Should().Be("#00B2A9");
    }

    [Fact]
    public async Task A_malformed_sender_address_is_rejected()
    {
        var client = await TenantAdminClientAsync();

        var put = await client.PutAsJsonAsync("/api/notifications/mail-settings", new
        {
            fromName = "X",
            fromAddress = "not-an-email",
            replyTo = (string?)null,
            enabled = true,
            brandColor = (string?)null,
            footerNote = (string?)null,
        });

        put.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await put.Content.ReadAsStringAsync()).Should().Contain("MAIL-002");
    }

    [Fact]
    public async Task Unauthenticated_caller_is_refused()
    {
        var response = await _client.GetAsync("/api/notifications/mail-settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
