using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Audit finding M-12: the ADT inbox must be an INBOX — a malformed message
/// (unsupported event type) is stored and marked Failed against the endpoint's
/// health, never bounced invisibly at the front door. Endpoint health cannot be
/// blind to a malformed storm.
/// </summary>
public sealed class AdtInboxTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);
    private sealed record IdResponse(Guid id);

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

        var slug = $"adt-lab-{Guid.NewGuid():N}"[..20];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "ADT Lab",
            adminEmail = $"qa@{slug}.test",
            adminDisplayName = "QA",
            adminPassword = "Adt-Lab-Pass-1!",
        })).EnsureSuccessStatusCode();

        var tenantLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"qa@{slug}.test",
            password = "Adt-Lab-Pass-1!",
        });
        var tenantAdmin = factory.CreateClient();
        tenantAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return tenantAdmin;
    }

    [Fact]
    public async Task A_malformed_event_type_is_stored_as_a_failed_message_not_bounced()
    {
        var admin = await TenantAdminClientAsync();

        var endpoint = (await (await admin.PostAsJsonAsync("/api/integration/endpoints", new
        {
            name = "HIS ADT",
            system = "His",
            protocol = "Hl7V2",
        })).Content.ReadFromJsonAsync<IdResponse>())!;

        var response = await admin.PostAsJsonAsync($"/api/integration/endpoints/{endpoint.id}/adt", new
        {
            dedupKey = "MSG-0001",
            messageType = "ADT^A99",
            rawPayload = "MSH|^~\\&|garbage",
            eventType = "Nonsense",
            patientRef = "PT-1",
            encounterRef = "ENC-1",
            unit = "ICU",
            eventAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        });

        // M-12: the inbox records the garbage instead of rejecting it invisibly.
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a malformed message is an ingest FAILURE on the record, not a request error");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("Failed");
        body.RootElement.GetProperty("error").GetString().Should().NotBeNullOrEmpty();

        var messages = await admin.GetAsync($"/api/integration/endpoints/{endpoint.id}/messages");
        messages.StatusCode.Should().Be(HttpStatusCode.OK);
        (await messages.Content.ReadAsStringAsync()).Should().Contain("MSG-0001",
            "the malformed message is on the endpoint's record");
    }
}
