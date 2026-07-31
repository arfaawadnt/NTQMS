using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Closes finding <b>VER-001</b>: the regulated flows, over the real HTTP
/// pipeline, against a real PostgreSQL database.
/// <para>
/// Each test here corresponds to a defect that escaped a green suite in v1.51.x
/// because the functional tests run on the in-memory provider, where row-level
/// security, foreign keys and CHECK constraints do not exist. They are written
/// to fail if the corresponding fix is reverted — that is the point of them.
/// </para>
/// <para>
/// They skip (rather than fail) when no migrated PostgreSQL is reachable, so a
/// developer without a database still gets a green local run; CI supplies
/// <c>QMS_ITEST_POSTGRES</c> and therefore always executes them.
/// </para>
/// </summary>
public sealed class RegulatedFlowRealDatabaseTests : IClassFixture<RealDatabaseWebAppFactory>, IAsyncLifetime
{
    private readonly RealDatabaseWebAppFactory _factory;
    private readonly string _slug = $"ver001-{Guid.CreateVersion7().ToString("N")[..10]}";
    private const string AdminPassword = "Ver-Test-Tenant-Pass-1!";

    public RegulatedFlowRealDatabaseTests(RealDatabaseWebAppFactory factory) => _factory = factory;

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Removes the tenant this test class created, so runs do not accumulate.</summary>
    public async Task DisposeAsync()
    {
        if (!_factory.Available)
        {
            return;
        }

        // Children first: the tenant FK is RESTRICT by design.
        await _factory.ExecuteAsync(
            """
            DELETE FROM qams.role_permission rp USING qams.role r
              WHERE rp.role_id = r.id AND r.tenant_id IN (SELECT id FROM saas.tenant WHERE identifier LIKE @slug);
            DELETE FROM qams.role WHERE tenant_id IN (SELECT id FROM saas.tenant WHERE identifier LIKE @slug);
            DELETE FROM qams.lov_entry WHERE tenant_id IN (SELECT id FROM saas.tenant WHERE identifier LIKE @slug);
            DELETE FROM qams.user_account WHERE tenant_id IN (SELECT id FROM saas.tenant WHERE identifier LIKE @slug);
            DELETE FROM qams.ref_counter WHERE tenant_id IN (SELECT id FROM saas.tenant WHERE identifier LIKE @slug);
            DELETE FROM qams.outbox_event WHERE tenant_id IN (SELECT id FROM saas.tenant WHERE identifier LIKE @slug);
            DELETE FROM saas.tenant WHERE identifier LIKE @slug;
            """,
            ("slug", _slug + "%"));
    }

    private async Task<string> PlatformTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = RealDatabaseWebAppFactory.PlatformAdminEmail,
            password = RealDatabaseWebAppFactory.PlatformAdminPassword,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the platform administrator is bootstrapped at startup; body: {0}",
            await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpResponseMessage> ProvisionAsync(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "VER-001 Probe Laboratory",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "VER-001 Admin",
            adminPassword = AdminPassword,
        });
    }

    /// <summary>
    /// SH-D2 regression. Provisioning writes the tenant, its administrator and
    /// its outbox events in ONE SaveChanges. The tenant foreign keys are raw
    /// SQL, so EF has no relationship for them and no reason to order the tenant
    /// insert first — before they were deferred to COMMIT this returned 500
    /// (PostgreSQL 23503), and the in-memory suite could not see it because it
    /// has no foreign keys at all.
    /// </summary>
    [SkippableFact]
    public async Task Provisioning_a_tenant_succeeds_against_real_foreign_keys()
    {
        Skip.IfNot(_factory.Available, _factory.Unavailable ?? "PostgreSQL unavailable");
        using var client = _factory.CreateClient();

        var response = await ProvisionAsync(client, await PlatformTokenAsync(client), _slug);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the tenant FKs are DEFERRABLE INITIALLY DEFERRED, so intra-transaction insert order "
            + "cannot break provisioning; body: {0}", await response.Content.ReadAsStringAsync());

        (await _factory.ScalarAsync<long>(
                "SELECT count(*) FROM saas.tenant WHERE identifier = @slug", ("slug", _slug)))
            .Should().Be(1);
    }

    /// <summary>
    /// SH-D1 regression. Signing in writes a tenant-stamped security event
    /// BEFORE any tenant context exists. Once <c>audit.security_event</c> gained
    /// RLS, its WITH CHECK refused that write and every sign-in returned 500 —
    /// invisible in-memory, because there is no RLS there.
    /// </summary>
    [SkippableFact]
    public async Task Signing_in_writes_its_security_event_through_row_level_security()
    {
        Skip.IfNot(_factory.Available, _factory.Unavailable ?? "PostgreSQL unavailable");
        using var client = _factory.CreateClient();
        (await ProvisionAsync(client, await PlatformTokenAsync(client), _slug))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = _slug,
            email = $"admin@{_slug}.test",
            password = AdminPassword,
        });

        login.StatusCode.Should().Be(HttpStatusCode.OK,
            "the login handler scopes the request tenant as soon as the slug resolves, so the "
            + "security-event write satisfies the RLS WITH CHECK; body: {0}",
            await login.Content.ReadAsStringAsync());

        (await _factory.ScalarAsync<long>(
                """
                SELECT count(*) FROM audit.security_event e
                JOIN saas.tenant t ON t.id = e.tenant_id
                WHERE t.identifier = @slug AND e.event_type = 'LOGIN_SUCCESS'
                """, ("slug", _slug)))
            .Should().BeGreaterThan(0, "the event is attributed to the tenant that signed in");
    }

    /// <summary>
    /// RP-D1 / URS-106 regression. An owned child carries a shadow tenant id that
    /// no CLR cast can see; before the interceptor read it, privilege-detail rows
    /// were written unattributed and were therefore invisible in the very
    /// tenant's compliance view. Proven end to end: provision, then read the
    /// ledger as that tenant's own administrator.
    /// </summary>
    [SkippableFact]
    public async Task Owned_child_changes_are_visible_in_the_owning_tenants_field_change_ledger()
    {
        Skip.IfNot(_factory.Available, _factory.Unavailable ?? "PostgreSQL unavailable");
        using var client = _factory.CreateClient();
        (await ProvisionAsync(client, await PlatformTokenAsync(client), _slug))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await _factory.ScalarAsync<long>(
                """
                SELECT count(*) FROM audit.field_change f
                JOIN saas.tenant t ON t.id = f.tenant_id
                WHERE t.identifier = @slug AND f.entity_type = 'RolePermission'
                """, ("slug", _slug)))
            .Should().BeGreaterThan(0,
                "seeded role permissions are owned children written on an elevated path; they must "
                + "still be attributed to the tenant, or its own ledger view cannot show them");

        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = _slug,
            email = $"admin@{_slug}.test",
            password = AdminPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var ledger = await client.GetAsync("/api/compliance/field-changes?take=500");
        ledger.StatusCode.Should().Be(HttpStatusCode.OK);

        var rows = await ledger.Content.ReadFromJsonAsync<JsonElement>();
        rows.EnumerateArray()
            .Any(r => r.GetProperty("entityType").GetString() == "RolePermission")
            .Should().BeTrue("the tenant must see its own privilege detail in its own ledger");
    }

    /// <summary>
    /// The isolation guarantee itself, over HTTP rather than SQL: a second
    /// tenant provisioned in the same database must be invisible. Exercises the
    /// EF filter, the RLS policy and the JWT tenant claim together — the
    /// combination no other functional test covers.
    /// </summary>
    [SkippableFact]
    public async Task A_tenant_sees_only_its_own_users_over_http()
    {
        Skip.IfNot(_factory.Available, _factory.Unavailable ?? "PostgreSQL unavailable");
        using var client = _factory.CreateClient();
        var platform = await PlatformTokenAsync(client);

        var other = _slug + "b";
        (await ProvisionAsync(client, platform, _slug)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProvisionAsync(client, platform, other)).StatusCode.Should().Be(HttpStatusCode.Created);

        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = _slug,
            email = $"admin@{_slug}.test",
            password = AdminPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var users = await client.GetAsync("/api/users");
        users.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await users.Content.ReadFromJsonAsync<JsonElement>();
        var emails = payload.EnumerateArray().Select(u => u.GetProperty("email").GetString()).ToList();

        emails.Should().Contain($"admin@{_slug}.test");
        emails.Should().NotContain($"admin@{other}.test",
            "another tenant's users must not be readable, by filter or by policy");
    }
}
