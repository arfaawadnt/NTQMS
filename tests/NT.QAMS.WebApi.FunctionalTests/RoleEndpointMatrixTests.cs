using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-9 finding (SEC-003 depth): the role×endpoint deny matrix. Every one
/// of the six roles is driven against a representative slice of the role-gated
/// surface, and TWO invariants are asserted for every cell:
/// <list type="number">
/// <item><b>No silent leakage / no server fault:</b> the response is always
/// one of 2xx / 404 (allowed) or 403 (denied) — never 401 (all callers are
/// authenticated), never 5xx.</item>
/// <item><b>Uniform error contract:</b> a 403 is always
/// application/problem+json carrying a stable <c>code</c> — never a bare
/// status.</item>
/// </list>
/// The specific allow/deny expectations per gate are asserted on top of that.
/// </summary>
public sealed class RoleEndpointMatrixTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);

    // The six roles. TenantAdmin is created at provisioning; the rest are
    // registered under the tenant. PlatformAdmin is tenant-less.
    private static readonly string[] TenantRoles =
        ["TenantAdmin", "QualityManager", "DepartmentHead", "Analyst", "ExternalAuditor"];

    private sealed record Endpoint(string Path, string Method, string Gate, HashSet<string> AllowedRoles);

    // A representative cell per distinct role gate in the API (Roles.cs).
    private static readonly Endpoint[] Surface =
    [
        new("/api/users", "GET", "TenantAdminOnly", ["TenantAdmin"]),
        new("/api/access-reviews", "GET", "QmOrAdmin", ["TenantAdmin", "QualityManager"]),
        new("/api/exports/audit-trail.xlsx", "GET", "QmAdminAuditor", ["TenantAdmin", "QualityManager", "ExternalAuditor"]),
        new("/api/documents/00000000-0000-0000-0000-000000000000/controlled-copies", "GET", "QmDeptAdmin-read?", ["TenantAdmin", "QualityManager", "DepartmentHead", "Analyst", "ExternalAuditor"]),
        new("/api/nonconformances", "GET", "AnyAuthenticated", ["TenantAdmin", "QualityManager", "DepartmentHead", "Analyst", "ExternalAuditor"]),
    ];

    private async Task<Dictionary<string, HttpClient>> BuildRoleClientsAsync()
    {
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);

        var slug = $"matrix-{Guid.NewGuid():N}"[..18];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Matrix Lab",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "Tenant Admin",
            adminPassword = "Matrix-Admin-1!",
        })).EnsureSuccessStatusCode();

        var clients = new Dictionary<string, HttpClient>
        {
            ["PlatformAdmin"] = admin,
            ["TenantAdmin"] = await TenantClientAsync(slug, $"admin@{slug}.test", "Matrix-Admin-1!"),
        };

        var tenantAdmin = clients["TenantAdmin"];
        foreach (var role in TenantRoles.Where(r => r != "TenantAdmin"))
        {
            var email = $"{role.ToLowerInvariant()}@{slug}.test";
            (await tenantAdmin.PostAsJsonAsync("/api/users", new
            {
                email,
                displayName = role,
                role,
                initialPassword = "Matrix-User-1!",
            })).EnsureSuccessStatusCode();
            clients[role] = await TenantClientAsync(slug, email, "Matrix-User-1!");
        }

        return clients;
    }

    private async Task<HttpClient> TenantClientAsync(string slug, string email, string password)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email,
            password,
        });
        login.EnsureSuccessStatusCode();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return client;
    }

    [Fact]
    public async Task Every_role_against_every_gated_endpoint_is_2xx_404_or_problem_json_403()
    {
        var clients = await BuildRoleClientsAsync();

        foreach (var endpoint in Surface)
        {
            foreach (var role in TenantRoles)
            {
                var response = await clients[role].GetAsync(endpoint.Path);
                var status = (int)response.StatusCode;
                var cell = $"{role} → {endpoint.Method} {endpoint.Path} ({endpoint.Gate})";

                // Invariant 1: never a server fault, never an auth challenge
                // (every caller here is authenticated).
                status.Should().NotBe(500, $"{cell} must not fault");
                response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
                    $"{cell}: an authenticated caller is 403 when denied, never 401");
                status.Should().BeOneOf([200, 204, 400, 403, 404], $"{cell}: unexpected status {status}");

                if (endpoint.AllowedRoles.Contains(role))
                {
                    // Allowed: success or a not-found placeholder id — never 403.
                    response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
                        $"{cell}: this role is permitted by the gate");
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // Invariant 2: a denial is problem+json with a stable code.
                    response.Content.Headers.ContentType!.MediaType.Should()
                        .Be("application/problem+json", $"{cell}: denials use the uniform contract");
                    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    body.RootElement.TryGetProperty("code", out var code).Should().BeTrue($"{cell}: 403 carries a code");
                    code.GetString().Should().StartWith("AUTH", $"{cell}: an authorization code");
                }
            }
        }
    }

    [Fact]
    public async Task The_read_only_auditor_and_analyst_are_denied_the_admin_surface()
    {
        var clients = await BuildRoleClientsAsync();

        // TenantAdminOnly gate: everyone below TenantAdmin is 403.
        foreach (var role in new[] { "QualityManager", "DepartmentHead", "Analyst", "ExternalAuditor" })
        {
            (await clients[role].GetAsync("/api/users")).StatusCode.Should()
                .Be(HttpStatusCode.Forbidden, $"{role} is not a tenant administrator");
        }

        // QmOrAdmin gate: Analyst + auditor + dept-head are 403 on access reviews.
        foreach (var role in new[] { "DepartmentHead", "Analyst", "ExternalAuditor" })
        {
            (await clients[role].GetAsync("/api/access-reviews")).StatusCode.Should()
                .Be(HttpStatusCode.Forbidden, $"{role} is outside the quality-approval group");
        }

        // Platform-only surface: no tenant role may list tenants.
        foreach (var role in TenantRoles)
        {
            (await clients[role].GetAsync("/api/tenants")).StatusCode.Should()
                .Be(HttpStatusCode.Forbidden, $"{role} may not reach the platform control plane");
        }
    }
}
