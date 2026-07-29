using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// The pre-authentication workspace lookup: the login page resolves a laboratory's
/// real NAME from the slug in its own sign-in address, instead of echoing the
/// identifier back at the user.
/// <para>
/// The endpoint is anonymous by necessity, so these tests also pin its disclosure
/// boundary: it answers with a name and nothing else, and an unknown or malformed
/// slug is indistinguishable from any other miss.
/// </para>
/// </summary>
public sealed class WorkspaceLookupTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private sealed record TokenResponse(string AccessToken);

    private sealed record Workspace(string Name);

    private async Task<HttpClient> PlatformAdminAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task An_active_laboratory_resolves_to_its_name_without_a_credential()
    {
        var admin = await PlatformAdminAsync();
        var slug = $"ws-{Guid.NewGuid():N}"[..12];
        const string LabName = "Amman Central Laboratory";

        var provision = await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = LabName,
            adminEmail = "admin@ws.test",
            adminDisplayName = "TA",
            adminPassword = "Tenant-Admin-Pass-1!",
        });
        provision.StatusCode.Should().Be(HttpStatusCode.Created);

        // Anonymous client: this runs before anyone has signed in.
        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/auth/workspace/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var workspace = await response.Content.ReadFromJsonAsync<Workspace>();
        workspace!.Name.Should().Be(LabName,
            "the login page must show the laboratory's name, not its identifier");
    }

    [Fact]
    public async Task The_response_carries_the_name_and_nothing_else()
    {
        var admin = await PlatformAdminAsync();
        var slug = $"ws-{Guid.NewGuid():N}"[..12];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Disclosure Boundary Lab",
            adminEmail = "admin@ws2.test",
            adminDisplayName = "TA",
            adminPassword = "Tenant-Admin-Pass-1!",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await factory.CreateClient().GetStringAsync($"/api/auth/workspace/{slug}");

        // The payload must be exactly one property: no tenant id, status or settings
        // may reach an unauthenticated caller.
        using var json = JsonDocument.Parse(body);
        json.RootElement.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo(["name"], "an anonymous endpoint discloses the name only");
        json.RootElement.GetProperty("name").GetString().Should().Be("Disclosure Boundary Lab");
    }

    [Theory]
    [InlineData("no-such-lab-here")]      // well-formed but unknown
    [InlineData("Not_A_Valid_Slug")]      // malformed
    [InlineData("x")]                     // too short for a slug
    public async Task Unknown_and_malformed_slugs_answer_alike(string slug)
    {
        var response = await factory.CreateClient().GetAsync($"/api/auth/workspace/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a miss must not reveal whether the slug merely does not exist, is malformed, or is inactive");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
