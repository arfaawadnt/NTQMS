using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NT.QAMS.WebApi.Middleware;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-4 finding CQRS-004 over the real pipeline: retrying an unsafe
/// command with the same Idempotency-Key replays the first response — the
/// classic double-submit nets exactly one nonconformance.
/// </summary>
public sealed class IdempotencyTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);
    private sealed record IdResponse(Guid id);

    private async Task<HttpClient> TenantClientAsync()
    {
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;

        var provisioner = factory.CreateClient();
        provisioner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);
        var slug = $"idem-lab-{Guid.NewGuid():N}"[..20];
        (await provisioner.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Idempotency Lab",
            adminEmail = $"qa@{slug}.test",
            adminDisplayName = "QA",
            adminPassword = "Idem-Lab-Pass-1!",
        })).EnsureSuccessStatusCode();

        var tenantLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"qa@{slug}.test",
            password = "Idem-Lab-Pass-1!",
        });
        var tenantToken = (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;

        var tenantClient = factory.CreateClient();
        tenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        return tenantClient;
    }

    [Fact]
    public async Task A_double_submit_with_the_same_key_nets_one_nonconformance()
    {
        var client = await TenantClientAsync();
        var key = $"retry-{Guid.NewGuid():N}";

        async Task<HttpResponseMessage> RaiseAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/nonconformances")
            {
                Content = JsonContent.Create(new
                {
                    title = "Duplicate-submit guard",
                    description = "raised twice with one Idempotency-Key",
                    severity = 3,
                    likelihood = 2,
                    sourceType = "Internal",
                }),
            };
            request.Headers.Add(HeaderIdempotencyKeyAccessor.HeaderName, key);
            return await client.SendAsync(request);
        }

        var first = await RaiseAsync();
        var second = await RaiseAsync();

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<IdResponse>())!.id;
        var secondId = (await second.Content.ReadFromJsonAsync<IdResponse>())!.id;
        secondId.Should().Be(firstId, "the retry replays the first execution's response");

        var list = await client.GetFromJsonAsync<List<Dictionary<string, object>>>("/api/nonconformances");
        list!.Count(nc => nc["title"].ToString() == "Duplicate-submit guard")
            .Should().Be(1, "exactly one NC exists despite two submits");
    }
}
