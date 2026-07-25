using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Locks down the day-one usability requirement: provisioning a tenant seeds
/// the starter list-of-values catalog for EVERY category the UI offers, so no
/// dropdown greets a new user empty. Runs over the real provisioning pipeline.
/// </summary>
public sealed class DefaultLovSeedingTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private sealed record AuthResponse(string accessToken);
    private sealed record LovRow(Guid id, string category, string code, bool isActive);

    /// <summary>Every LOV category surfaced by a dropdown in the frontend.</summary>
    private static readonly string[] UiCategories =
    [
        "DOC_CATEGORY", "RISK_CATEGORY", "SUPPLIER_TYPE", "CERTIFICATE_TYPE", "PT_SCHEME",
        "EQUIPMENT_LOCATION", "INTERMEDIATE_CHECK_TYPE", "ENV_PARAMETER",
        "FEEDBACK_SOURCE", "FEEDBACK_CHANNEL", "INTERESTED_PARTY_CATEGORY", "CONTEXT_ISSUE_CATEGORY",
    ];

    [Fact]
    public async Task Provisioning_a_tenant_seeds_starter_values_for_every_ui_category()
    {
        var platform = factory.CreateClient();
        var login = await platform.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        platform.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        var slug = $"lov-{Guid.NewGuid():N}"[..12];
        var provision = await platform.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "LOV Seed Lab",
            adminEmail = "lov-admin@functional.test",
            adminDisplayName = "LOV Admin",
            adminPassword = "Tenant-Admin-Pass-1!",
        });
        provision.StatusCode.Should().Be(HttpStatusCode.Created);

        var tenant = factory.CreateClient();
        var tenantLogin = await tenant.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = "lov-admin@functional.test",
            password = "Tenant-Admin-Pass-1!",
        });
        tenantLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        tenant.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        var all = await tenant.GetFromJsonAsync<List<LovRow>>("/api/lovs");
        all.Should().NotBeNull();

        foreach (var category in UiCategories)
        {
            all!.Where(l => l.category == category && l.isActive).Should().NotBeEmpty(
                because: $"a new tenant must not face an empty {category} dropdown");
        }
    }
}
