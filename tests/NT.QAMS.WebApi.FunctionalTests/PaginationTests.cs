using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-4 finding API-004 over the real pipeline: list endpoints answer with
/// the pagination envelope — correct page slicing, a true total, hasMore
/// navigation, and a clamped page size instead of an unbounded query.
/// </summary>
public sealed class PaginationTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);

    private async Task<HttpClient> TenantClientWithNcsAsync(int count)
    {
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;

        var provisioner = factory.CreateClient();
        provisioner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);
        var slug = $"page-lab-{Guid.NewGuid():N}"[..20];
        (await provisioner.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Pagination Lab",
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
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        for (var i = 1; i <= count; i++)
        {
            (await client.PostAsJsonAsync("/api/nonconformances", new
            {
                title = $"Paged NC {i:00}",
                description = "pagination fixture",
                severity = 2,
                likelihood = 2,
                sourceType = "Internal",
            })).EnsureSuccessStatusCode();
        }

        return client;
    }

    [Fact]
    public async Task Pages_slice_navigate_and_report_the_true_total()
    {
        var client = await TenantClientWithNcsAsync(count: 3);

        var first = await client.GetFromJsonAsync<JsonElement>("/api/nonconformances?page=1&pageSize=2");
        first.GetProperty("items").GetArrayLength().Should().Be(2);
        first.GetProperty("total").GetInt32().Should().Be(3, "the client sees the full filtered count");
        first.GetProperty("hasMore").GetBoolean().Should().BeTrue();

        var second = await client.GetFromJsonAsync<JsonElement>("/api/nonconformances?page=2&pageSize=2");
        second.GetProperty("items").GetArrayLength().Should().Be(1);
        second.GetProperty("hasMore").GetBoolean().Should().BeFalse();

        // Navigation covers the whole set exactly once (stable ordering).
        var firstIds = first.GetProperty("items").EnumerateArray()
            .Select(nc => nc.GetProperty("id").GetString()).ToList();
        var secondIds = second.GetProperty("items").EnumerateArray()
            .Select(nc => nc.GetProperty("id").GetString()).ToList();
        firstIds.Concat(secondIds).Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_not_an_error()
    {
        var client = await TenantClientWithNcsAsync(count: 1);

        var beyond = await client.GetFromJsonAsync<JsonElement>("/api/nonconformances?page=99&pageSize=50");

        beyond.GetProperty("items").GetArrayLength().Should().Be(0);
        beyond.GetProperty("total").GetInt32().Should().Be(1);
        beyond.GetProperty("hasMore").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_hostile_page_size_is_clamped()
    {
        var client = await TenantClientWithNcsAsync(count: 1);

        var response = await client.GetFromJsonAsync<JsonElement>("/api/nonconformances?page=1&pageSize=100000");

        response.GetProperty("pageSize").GetInt32().Should().BeLessThanOrEqualTo(200,
            "a client can never turn a list endpoint into an unbounded query");
    }
}
