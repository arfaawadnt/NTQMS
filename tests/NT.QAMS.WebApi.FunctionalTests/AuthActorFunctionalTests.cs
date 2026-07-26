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
    private sealed record EnrollAuthResponse(string accessToken, bool mfaEnrollmentRequired);
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
    public async Task Privileged_user_without_mfa_is_gated_to_enrollment_when_enforced()
    {
        // Flip F-04 on for this host only (default off elsewhere).
        var mfaOn = factory.WithWebHostBuilder(b =>
            b.UseSetting("Security:RequireMfaForPrivilegedRoles", "true"));
        var client = mfaOn.CreateClient();

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginRes.Content.ReadFromJsonAsync<EnrollAuthResponse>();
        login!.mfaEnrollmentRequired.Should().BeTrue("a privileged user without MFA must be forced to enrol");
        login.accessToken.Should().NotBeNullOrEmpty();

        Authorize(client, login.accessToken);

        // The enrollment-scoped session is refused everywhere…
        var blocked = await client.GetAsync("/api/tenants");
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // …except the MFA-enrollment endpoint, so the user can break the deadlock.
        var enroll = await client.PostAsync("/api/auth/mfa/enroll", null);
        enroll.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tenant_can_opt_into_enforced_mfa_for_its_privileged_users()
    {
        // Provision a tenant — MFA is OFF by default.
        var admin = factory.CreateClient();
        Authorize(admin, await PlatformTokenAsync());
        var slug = $"mfa-{Guid.NewGuid():N}"[..12];
        var provision = await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "MFA Opt-in Lab",
            adminEmail = "admin@mfa.test",
            adminDisplayName = "TA",
            adminPassword = "Tenant-Admin-Pass-1!",
        });
        provision.StatusCode.Should().Be(HttpStatusCode.Created);

        var ta = factory.CreateClient();
        async Task<EnrollAuthResponse> LoginAsync() =>
            (await (await ta.PostAsJsonAsync("/api/auth/login", new
            {
                tenantIdentifier = slug,
                email = "admin@mfa.test",
                password = "Tenant-Admin-Pass-1!",
            })).Content.ReadFromJsonAsync<EnrollAuthResponse>())!;

        var before = await LoginAsync();
        before.mfaEnrollmentRequired.Should().BeFalse("a new tenant does not enforce MFA");

        // The tenant admin opts THEIR tenant in.
        Authorize(ta, before.accessToken);
        var set = await ta.PutAsJsonAsync("/api/tenant-settings/mfa-policy", new { require = true });
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Now a privileged login for that tenant is gated to enrollment.
        var after = await LoginAsync();
        after.mfaEnrollmentRequired.Should().BeTrue("the tenant opted into enforced MFA");
    }

    [Fact]
    public async Task Deactivating_a_user_revokes_their_live_session_immediately()
    {
        // Provision a tenant + admin, who then registers a second user.
        var admin = factory.CreateClient();
        Authorize(admin, await PlatformTokenAsync());
        var slug = $"rev-{Guid.NewGuid():N}"[..12];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug, name = "Revocation Lab",
            adminEmail = "admin@rev.test", adminDisplayName = "TA", adminPassword = "Tenant-Admin-Pass-1!",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var ta = factory.CreateClient();
        var taLogin = await (await ta.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug, email = "admin@rev.test", password = "Tenant-Admin-Pass-1!",
        })).Content.ReadFromJsonAsync<AuthResponse>();
        Authorize(ta, taLogin!.accessToken);

        var reg = await ta.PostAsJsonAsync("/api/users", new
        {
            email = "analyst@rev.test", displayName = "Analyst", role = "Analyst", initialPassword = "Analyst-Pass-1!",
        });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await reg.Content.ReadFromJsonAsync<IdResponse>())!.id;

        // The analyst signs in and their token works.
        var analyst = factory.CreateClient();
        var aLogin = await (await analyst.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug, email = "analyst@rev.test", password = "Analyst-Pass-1!",
        })).Content.ReadFromJsonAsync<AuthResponse>();
        Authorize(analyst, aLogin!.accessToken);
        (await analyst.GetAsync("/api/notifications/mine")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The admin deactivates the analyst — the SAME token must now be refused.
        (await ta.PostAsync($"/api/users/{userId}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await analyst.GetAsync("/api/notifications/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
