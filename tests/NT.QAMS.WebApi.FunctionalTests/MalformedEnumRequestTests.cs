using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Audit finding M-11: the new controllers convert request strings to domain
/// enums at the boundary. A typo ("NotACategory") used to escape as an
/// unhandled <see cref="ArgumentException"/> — HTTP 500 — and a numeric string
/// ("7777") used to parse into an UNDEFINED enum value that sailed straight to
/// the database. Both are the client's error: the contract pinned here is a
/// 400 problem+json carrying the stable code <c>REQ-001</c>.
/// </summary>
public sealed class MalformedEnumRequestTests(QamsWebAppFactory factory)
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

        var slug = $"enum-lab-{Guid.NewGuid():N}"[..20];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Enum Lab",
            adminEmail = $"qa@{slug}.test",
            adminDisplayName = "QA",
            adminPassword = "Enum-Lab-Pass-1!",
        })).EnsureSuccessStatusCode();

        var tenantLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"qa@{slug}.test",
            password = "Enum-Lab-Pass-1!",
        });
        var tenantAdmin = factory.CreateClient();
        tenantAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return tenantAdmin;
    }

    private static object IncidentPayload(string category) => new
    {
        title = "Malformed-enum probe",
        description = "M-11: the boundary must reject this before the domain sees it.",
        category,
        harmGrade = "NoHarm",
        channel = "Web",
        occurredAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
    };

    [Theory]
    [InlineData("NotACategory", "a name that is not in the enum")]
    [InlineData("7777", "a numeric string outside the defined values")]
    public async Task A_malformed_enum_value_is_a_400_problem_not_a_500_or_a_silent_write(
        string category, string why)
    {
        var admin = await TenantAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/incidents", IncidentPayload(category));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"M-11: {why} is the client's error — never a 500, never persisted");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("REQ-001");

        // Control: the same payload with a defined name is accepted.
        var ok = await admin.PostAsJsonAsync("/api/incidents", IncidentPayload("Fall"));
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
