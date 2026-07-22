using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Reproduces and locks down the v1.0 deployment defect: with the actor's id
/// carried in the JWT "sub" claim, every handler that needs the current user
/// (raise NC, my-notifications) must work over the real HTTP + JWT pipeline —
/// not just against the fake current-user the unit tests use.
/// </summary>
public sealed class AuthActorFunctionalTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private sealed record AuthResponse(
        string accessToken, string role, string displayName, string? tenantId, bool mfaRequired);
    private sealed record IdResponse(Guid id);

    private readonly HttpClient _client = factory.CreateClient();

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> PlatformTokenAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        body!.role.Should().Be("PlatformAdmin");
        body.accessToken.Should().NotBeNullOrEmpty();
        return body.accessToken;
    }

    [Fact]
    public async Task Anonymous_request_is_denied()
    {
        var res = await _client.GetAsync("/api/tenants");
        var serverErrors = string.Join(" | ", factory.ServerErrors);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized, because: $"server errors: {serverErrors}");
    }

    [Fact]
    public async Task Platform_admin_role_gate_allows_tenant_admin_endpoints()
    {
        var client = factory.CreateClient();
        Authorize(client, await PlatformTokenAsync());

        // Reading the tenant list requires the PlatformAdmin role — proves the
        // role claim round-trips through the JWT.
        var res = await client.GetAsync("/api/tenants");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Actor_scoped_flow_works_end_to_end_over_real_jwt()
    {
        // 1. Platform admin provisions a tenant + its admin.
        var admin = factory.CreateClient();
        Authorize(admin, await PlatformTokenAsync());

        var slug = $"lab-{Guid.NewGuid():N}".Substring(0, 12);
        var provision = await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Functional Test Lab",
            adminEmail = "qa@functional.test",
            adminDisplayName = "QA Manager",
            adminPassword = "Tenant-Admin-Pass-1!",
        });
        provision.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Tenant admin logs in — a real JWT with sub + tenant_id + role claims.
        var tenant = factory.CreateClient();
        var loginRes = await tenant.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = "qa@functional.test",
            password = "Tenant-Admin-Pass-1!",
        });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Authorize(tenant, login!.accessToken);

        // 3. THE REGRESSION: this endpoint reads the current user's id from the
        //    "sub" claim. Before the fix it returned 401 (UserId null); now 200.
        var notifications = await tenant.GetAsync("/api/notifications/mine");
        notifications.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Raising an NC also needs the actor — the exact action that failed
        //    in the deployed app.
        var raise = await tenant.PostAsJsonAsync("/api/nonconformances", new
        {
            title = "Functional test NC",
            description = "raised over the real pipeline",
            severity = 3,
            likelihood = 2,
            sourceType = "Internal",
        });
        raise.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await raise.Content.ReadFromJsonAsync<IdResponse>();
        created!.id.Should().NotBe(Guid.Empty);

        // 5. It is persisted and tenant-scoped (the stamp interceptor ran).
        var list = await tenant.GetFromJsonAsync<List<Dictionary<string, object>>>("/api/nonconformances");
        list.Should().NotBeNull();
        list!.Should().HaveCountGreaterThanOrEqualTo(1);
    }
}
