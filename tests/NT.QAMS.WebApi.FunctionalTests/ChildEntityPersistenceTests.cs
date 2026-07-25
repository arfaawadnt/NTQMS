using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Locks down the child-entity persistence defect found live on PostgreSQL:
/// EF's Guid-key convention (ValueGeneratedOnAdd) made change detection treat a
/// child appended to an ALREADY-PERSISTED aggregate as an existing row — the
/// key was set by the domain constructor — so it issued an UPDATE that affected
/// zero rows and threw DbUpdateConcurrencyException. Every add went through a
/// separate HTTP request here, exactly the request-per-step shape production
/// traffic has, which single-scope unit tests can never reproduce.
/// </summary>
public sealed class ChildEntityPersistenceTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private sealed record AuthResponse(string accessToken);
    private sealed record IdResponse(Guid id);
    private sealed record BudgetDetail(
        Guid id, string status, decimal? combinedStandardUncertainty,
        decimal? expandedUncertainty, bool? meetsTarget, List<ComponentDto> components);
    private sealed record ComponentDto(Guid id, string name, string type, decimal relativeStandardUncertainty);

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Children_added_to_a_reloaded_aggregate_persist_across_requests()
    {
        // Provision a tenant and sign in as its admin — real JWT pipeline.
        var platform = factory.CreateClient();
        var login = await platform.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        Authorize(platform, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        var slug = $"mu-{Guid.NewGuid():N}"[..12];
        var provision = await platform.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "MU Functional Lab",
            adminEmail = "mu-admin@functional.test",
            adminDisplayName = "MU Admin",
            adminPassword = "Tenant-Admin-Pass-1!",
        });
        provision.StatusCode.Should().Be(HttpStatusCode.Created);

        var client = factory.CreateClient();
        var tenantLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = "mu-admin@functional.test",
            password = "Tenant-Admin-Pass-1!",
        });
        tenantLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        Authorize(client, (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        // Request 1: create the aggregate. Requests 2–3: reload it and append a
        // child each time — the exact shape that produced 0-row UPDATEs live.
        var create = await client.PostAsJsonAsync("/api/uncertainty-budgets", new
        {
            analyte = "Glucose",
            method = "Hexokinase",
            unit = "mmol/L",
            level = "5.5 mmol/L",
            coverageFactor = 2,
            targetExpandedUncertainty = 10,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created,
            because: $"server errors: {string.Join(" | ", factory.ServerErrors)}");
        var budgetId = (await create.Content.ReadFromJsonAsync<IdResponse>())!.id;

        var comp1 = await client.PostAsJsonAsync($"/api/uncertainty-budgets/{budgetId}/components", new
        {
            name = "Repeatability (QC CV)",
            type = "TypeA",
            relativeStandardUncertainty = 3,
            source = "QC lot 77",
        });
        comp1.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"a child added to a reloaded aggregate must INSERT, not UPDATE; server errors: {string.Join(" | ", factory.ServerErrors)}");

        var comp2 = await client.PostAsJsonAsync($"/api/uncertainty-budgets/{budgetId}/components", new
        {
            name = "Bias (PT)",
            type = "TypeB",
            relativeStandardUncertainty = 4,
            source = "EQAS 2026-A/B",
        });
        comp2.StatusCode.Should().Be(HttpStatusCode.OK);

        // GUM math over the persisted children: u_c = √(3²+4²) = 5, U = 2·5 = 10.
        var calc = await client.PostAsync($"/api/uncertainty-budgets/{budgetId}/calculate", null);
        calc.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: $"both components must be visible after reload; server errors: {string.Join(" | ", factory.ServerErrors)}");

        var detail = await client.GetFromJsonAsync<BudgetDetail>($"/api/uncertainty-budgets/{budgetId}");
        detail!.components.Should().HaveCount(2);
        detail.combinedStandardUncertainty.Should().Be(5m);
        detail.expandedUncertainty.Should().Be(10m);
        detail.meetsTarget.Should().BeTrue();
        detail.status.Should().Be("Calculated");
    }
}
