using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// URS-132 over the real pipeline: the unified "My Tasks" action centre aggregates
/// pending actions for the signed-in user. A manual task assigned to the caller
/// must appear in the feed (proving the live read model unions at least the task
/// source), and an unauthenticated caller is refused.
/// </summary>
public sealed class MyActionsTests(QamsWebAppFactory factory)
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

        var slug = $"acts-{Guid.NewGuid():N}"[..18];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Actions Lab",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "Tenant Admin",
            adminPassword = "Acts-Admin-1!",
        })).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"admin@{slug}.test",
            password = "Acts-Admin-1!",
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return client;
    }

    [Fact]
    public async Task A_task_assigned_to_the_caller_appears_in_the_action_centre()
    {
        var client = await TenantAdminClientAsync();

        // Routed to the caller's structural tier; the feed resolves it from the DB.
        var create = await client.PostAsJsonAsync("/api/tasks", new
        {
            subject = "Review the calibration SOP",
            subjectRef = "DOC-SOP-001",
            assigneeUserId = (Guid?)null,
            assigneeRole = "TenantAdmin",
            dueDate = "2026-09-01",
        });
        create.EnsureSuccessStatusCode();

        var feed = await client.GetFromJsonAsync<List<JsonElement>>("/api/tasks/my-actions");

        feed.Should().NotBeNull();
        feed!.Should().Contain(i =>
            i.GetProperty("category").GetString() == "task"
            && i.GetProperty("title").GetString() == "Review the calibration SOP");
    }

    [Fact]
    public async Task Unauthenticated_caller_is_refused()
    {
        var response = await _client.GetAsync("/api/tasks/my-actions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
