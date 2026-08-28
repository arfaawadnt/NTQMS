using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// The Role Privilege module end to end, over HTTP: seeded roles at
/// provisioning, a custom role whose grants flip real endpoints between 403 and
/// allowed, the last-administrator lockout guard, the working-scope hard data
/// filter, and the actor's own <c>me/privileges</c> surface.
/// </summary>
public sealed class RolePrivilegeFlowTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);
    private sealed record IdResponse(Guid id);
    private sealed record RoleRow(Guid id, string name, bool isSystem, bool isActive, int permissionCount, int memberCount);
    private sealed record RoleDetail(Guid id, string name, IReadOnlyList<string> permissionKeys);
    private sealed record MyPrivileges(
        Guid? roleId, string? roleName, bool isPlatformAdmin,
        IReadOnlyList<string> permissions, IReadOnlyList<Guid> branchIds, string? preferredLanguage);
    private sealed record NcRow(Guid id, string title);
    private sealed record NcPage(List<NcRow> items);

    private async Task<HttpClient> LoginAsync(string? slug, string email, string password)
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

    private async Task<(string Slug, HttpClient Admin)> ProvisionAsync()
    {
        var platform = await LoginAsync(null, QamsWebAppFactory.PlatformAdminEmail, QamsWebAppFactory.PlatformAdminPassword);
        var slug = $"priv-{Guid.NewGuid():N}"[..16];
        (await platform.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Privilege Lab",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "Admin",
            adminPassword = "Privilege-Admin-1!",
        })).EnsureSuccessStatusCode();
        return (slug, await LoginAsync(slug, $"admin@{slug}.test", "Privilege-Admin-1!"));
    }

    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Provisioning_seeds_the_five_system_roles_and_the_catalog_renders()
    {
        var (_, admin) = await ProvisionAsync();

        var roles = (await admin.GetFromJsonAsync<List<RoleRow>>("/api/roles"))!;
        roles.Should().HaveCount(5);
        roles.Should().OnlyContain(r => r.isSystem && r.isActive);
        roles.Single(r => r.name == "Tenant Administrator").memberCount.Should().Be(1);

        var catalog = await admin.GetAsync("/api/roles/catalog");
        catalog.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await catalog.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("modules").GetArrayLength().Should().BeGreaterThan(25);

        // M-07: the HQMS grant decisions are visible over HTTP exactly as seeded.
        var deptHeadId = roles.Single(r => r.name == "Department Head").id;
        var deptHead = (await admin.GetFromJsonAsync<RoleDetail>($"/api/roles/{deptHeadId}"))!;
        deptHead.permissionKeys.Should().Contain("patient-safety.create")
            .And.Contain("credentialing.view")
            .And.NotContain("integration.view");

        var auditorId = roles.Single(r => r.name == "External Auditor").id;
        var auditorRole = (await admin.GetFromJsonAsync<RoleDetail>($"/api/roles/{auditorId}"))!;
        auditorRole.permissionKeys.Should().Contain("incidents.view")
            .And.NotContain("patient-safety.view")
            .And.NotContain("integration.view");
    }

    [Fact]
    public async Task A_custom_roles_grants_flip_real_endpoints_between_denied_and_allowed()
    {
        var (slug, admin) = await ProvisionAsync();

        // A role that may only read NCs.
        var role = (await (await admin.PostAsJsonAsync("/api/roles", new
        {
            name = "NC Reader",
            description = "Sees nonconformances, changes nothing.",
            permissionKeys = new[] { "nc.view" },
        })).Content.ReadFromJsonAsync<IdResponse>())!;

        (await admin.PostAsJsonAsync("/api/users", new
        {
            email = $"reader@{slug}.test",
            displayName = "Reader",
            role = "Analyst",
            initialPassword = "Reader-Pass-1!",
            roleId = role.id,
        })).EnsureSuccessStatusCode();
        var reader = await LoginAsync(slug, $"reader@{slug}.test", "Reader-Pass-1!");

        // Denied: scheduling an audit needs audits.create.
        var denied = await reader.PostAsJsonAsync("/api/audits", new
        {
            title = "Unauthorised audit",
            scope = "QMS",
            leadAuditorId = Guid.NewGuid(),
            plannedDate = "2026-09-01",
            checklist = Array.Empty<object>(),
        });
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOf(denied)).Should().Be("AUTHZ-403");

        // Grant audits.create with a recorded reason - next request is admitted.
        (await admin.PutAsJsonAsync($"/api/roles/{role.id}/permissions", new
        {
            permissionKeys = new[] { "nc.view", "audits.view", "audits.create" },
            reason = "Readers now schedule their own department audits.",
        })).EnsureSuccessStatusCode();

        var allowed = await reader.PostAsJsonAsync("/api/audits", new
        {
            title = "Authorised audit",
            scope = "QMS",
            leadAuditorId = Guid.NewGuid(),
            plannedDate = "2026-09-01",
            checklist = Array.Empty<object>(),
        });
        allowed.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "the grant must take effect on the very next request, not at token expiry");

        // The actor sees their own effective privileges.
        var mine = (await reader.GetFromJsonAsync<MyPrivileges>("/api/auth/me/privileges"))!;
        mine.roleName.Should().Be("NC Reader");
        mine.isPlatformAdmin.Should().BeFalse();
        mine.permissions.Should().Contain("audits.create").And.Contain("nc.view");
    }

    [Fact]
    public async Task An_unknown_permission_key_is_rejected_when_saving_a_role()
    {
        var (_, admin) = await ProvisionAsync();

        var response = await admin.PostAsJsonAsync("/api/roles", new
        {
            name = "Ghost",
            permissionKeys = new[] { "nc.frobnicate" },
        });

        ((int)response.StatusCode).Should().BeOneOf(400, 422);
        (await CodeOf(response)).Should().Be("ROLE-005");
    }

    [Fact]
    public async Task The_tenant_cannot_lock_itself_out_of_privilege_administration()
    {
        var (_, admin) = await ProvisionAsync();
        var roles = (await admin.GetFromJsonAsync<List<RoleRow>>("/api/roles"))!;
        var adminRole = roles.Single(r => r.name == "Tenant Administrator");
        var detail = (await admin.GetFromJsonAsync<RoleDetail>($"/api/roles/{adminRole.id}"))!;

        var response = await admin.PutAsJsonAsync($"/api/roles/{adminRole.id}/permissions", new
        {
            permissionKeys = detail.permissionKeys.Where(k => k != "roles.manage").ToArray(),
            reason = "Attempting to drop privilege administration entirely.",
        });

        ((int)response.StatusCode).Should().BeOneOf(400, 409, 422);
        (await CodeOf(response)).Should().Be("ROLE-006");
    }

    [Fact]
    public async Task The_working_scope_is_a_hard_data_filter_on_reads_and_writes()
    {
        var (slug, admin) = await ProvisionAsync();

        var branchA = (await (await admin.PostAsJsonAsync("/api/branches", new { code = "A", name = "Branch A", city = "Amman" }))
            .Content.ReadFromJsonAsync<IdResponse>())!.id;
        var branchB = (await (await admin.PostAsJsonAsync("/api/branches", new { code = "B", name = "Branch B", city = "Irbid" }))
            .Content.ReadFromJsonAsync<IdResponse>())!.id;

        async Task<Guid> RaiseAsync(string title, Guid? branchId) =>
            (await (await admin.PostAsJsonAsync("/api/nonconformances", new
            {
                title,
                description = "Scope filter probe.",
                severity = 2,
                likelihood = 2,
                sourceType = "Internal",
                branchId,
            })).Content.ReadFromJsonAsync<IdResponse>())!.id;

        var inA = await RaiseAsync("NC in branch A", branchA);
        var inB = await RaiseAsync("NC in branch B", branchB);
        var unattributed = await RaiseAsync("NC without a branch", null);

        // A user confined to branch A.
        (await admin.PostAsJsonAsync("/api/users", new
        {
            email = $"scoped@{slug}.test",
            displayName = "Scoped Analyst",
            role = "QualityManager",
            initialPassword = "Scoped-Pass-1!",
        })).EnsureSuccessStatusCode();
        var users = await admin.GetFromJsonAsync<List<JsonElement>>("/api/users");
        var scopedId = users!.Single(u => u.GetProperty("email").GetString()!.StartsWith("scoped@"))
            .GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/users/{scopedId}/scope", new
        {
            branchIds = new[] { branchA },
            departmentIds = Array.Empty<Guid>(),
        })).EnsureSuccessStatusCode();

        var scoped = await LoginAsync(slug, $"scoped@{slug}.test", "Scoped-Pass-1!");

        // Reads: branch B's record does not exist for this user; unattributed stays.
        var visible = (await scoped.GetFromJsonAsync<NcPage>("/api/nonconformances?pageSize=100"))!.items;
        var ids = visible.Select(n => n.id).ToHashSet();
        ids.Should().Contain(inA).And.Contain(unattributed);
        ids.Should().NotContain(inB, "a branch-restricted user must never see another branch's records");

        (await scoped.GetAsync($"/api/nonconformances/{inB}")).StatusCode
            .Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity);

        // Writes: creating into the out-of-scope branch is refused in-transaction.
        var outOfScope = await scoped.PostAsJsonAsync("/api/nonconformances", new
        {
            title = "Attempt to write into branch B",
            description = "Should be stopped by the scope guard.",
            severity = 2,
            likelihood = 2,
            sourceType = "Internal",
            branchId = branchB,
        });
        ((int)outOfScope.StatusCode).Should().BeOneOf(400, 403, 422);
        (await CodeOf(outOfScope)).Should().Be("SCOPE-001");

        // The admin (unrestricted) still sees everything.
        var adminVisible = (await admin.GetFromJsonAsync<NcPage>("/api/nonconformances?pageSize=100"))!.items;
        adminVisible.Select(n => n.id).Should().Contain([inA, inB, unattributed]);
    }
}
