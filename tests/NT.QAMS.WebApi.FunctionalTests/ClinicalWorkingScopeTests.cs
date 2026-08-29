using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Audit finding M-02 (Group C — adopt IAllocatable): the patient-level clinical
/// registers now join the working-scope hard data filter. A branch-restricted
/// user sees the incidents of their branch and unattributed incidents, never
/// another branch's — the same guarantee the laboratory registers already had.
/// </summary>
public sealed class ClinicalWorkingScopeTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);
    private sealed record IdResponse(Guid id);

    private async Task<HttpClient> LoginAsync(string slug, string email, string password)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { tenantIdentifier = slug, email, password });
        login.EnsureSuccessStatusCode();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return client;
    }

    [Fact]
    public async Task A_branch_restricted_user_sees_only_their_branch_and_unattributed_incidents()
    {
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var admin0 = factory.CreateClient();
        admin0.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        var slug = $"cscope-{Guid.NewGuid():N}"[..18];
        (await admin0.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Clinical Scope Lab",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "Admin",
            adminPassword = "Cscope-Admin-1!",
        })).EnsureSuccessStatusCode();
        var admin = await LoginAsync(slug, $"admin@{slug}.test", "Cscope-Admin-1!");

        var branchA = (await (await admin.PostAsJsonAsync("/api/branches", new { code = "A", name = "Branch A", city = "Amman" }))
            .Content.ReadFromJsonAsync<IdResponse>())!.id;
        var branchB = (await (await admin.PostAsJsonAsync("/api/branches", new { code = "B", name = "Branch B", city = "Irbid" }))
            .Content.ReadFromJsonAsync<IdResponse>())!.id;

        async Task<Guid> ReportAsync(string title, Guid? branchId) =>
            (await (await admin.PostAsJsonAsync("/api/incidents", new
            {
                title, description = "scope probe", category = "Fall", harmGrade = "Minor", channel = "Web",
                occurredAtUtc = DateTimeOffset.UtcNow.AddHours(-1), branchId,
            })).Content.ReadFromJsonAsync<IdResponse>())!.id;

        var inA = await ReportAsync("Incident in A", branchA);
        var inB = await ReportAsync("Incident in B", branchB);
        var unattributed = await ReportAsync("Incident with no branch", null);

        (await admin.PostAsJsonAsync("/api/users", new
        {
            email = $"scoped@{slug}.test", displayName = "Scoped", role = "QualityManager",
            initialPassword = "Scoped-Pass-1!",
        })).EnsureSuccessStatusCode();
        var users = await admin.GetFromJsonAsync<List<JsonElement>>("/api/users");
        var scopedId = users!.Single(u => u.GetProperty("email").GetString()!.StartsWith("scoped@"))
            .GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/users/{scopedId}/scope", new
        {
            branchIds = new[] { branchA }, departmentIds = Array.Empty<Guid>(),
        })).EnsureSuccessStatusCode();

        var scoped = await LoginAsync(slug, $"scoped@{slug}.test", "Scoped-Pass-1!");

        using var doc = JsonDocument.Parse(await (await scoped.GetAsync("/api/incidents?pageSize=100")).Content.ReadAsStringAsync());
        var ids = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid()).ToHashSet();

        ids.Should().Contain(inA).And.Contain(unattributed);
        ids.Should().NotContain(inB, "a branch-restricted user must never see another branch's clinical records (M-02)");
    }
}
