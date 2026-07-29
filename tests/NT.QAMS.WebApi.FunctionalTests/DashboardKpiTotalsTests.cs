using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// The dashboard shows each KPI as a proportion of the population it is drawn
/// from ("1 of 9 tasks overdue"), so the denominators are load-bearing: if a
/// total were smaller than its own subset, or absent, the meter would be a lie.
/// These tests pin the invariant at the contract boundary.
/// </summary>
public sealed class DashboardKpiTotalsTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private sealed record TokenResponse(string AccessToken);

    private async Task<HttpClient> TenantAdminAsync()
    {
        var admin = factory.CreateClient();
        var platform = await admin.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        platform.StatusCode.Should().Be(HttpStatusCode.OK);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await platform.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);

        var slug = $"kpi-{Guid.NewGuid():N}"[..12];
        const string Password = "Tenant-Admin-Pass-1!";
        var email = $"admin@{slug}.test";
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "KPI Totals Lab",
            adminEmail = email,
            adminDisplayName = "TA",
            adminPassword = Password,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var tenant = factory.CreateClient();
        var login = await tenant.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email,
            password = Password,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        tenant.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        return tenant;
    }

    private static async Task<JsonElement> KpisAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/reports/kpis");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Every_kpi_reports_the_population_it_is_drawn_from()
    {
        var kpis = await KpisAsync(await TenantAdminAsync());

        kpis.TryGetProperty("totals", out var totals).Should().BeTrue(
            "the dashboard cannot render a proportion without the population");

        foreach (var field in new[]
                 {
                     "nonconformances", "capaActions", "complaints", "audits", "equipmentItems",
                     "risks", "workTasks", "ptEnrollments", "trainingAssignments", "suppliers", "documents",
                 })
        {
            totals.TryGetProperty(field, out var value).Should().BeTrue($"'{field}' is a denominator the UI relies on");
            value.GetInt32().Should().BeGreaterThanOrEqualTo(0, $"'{field}' is a row count");
        }
    }

    [Fact]
    public async Task No_kpi_ever_exceeds_its_own_population()
    {
        var kpis = await KpisAsync(await TenantAdminAsync());
        var totals = kpis.GetProperty("totals");

        // Each KPI paired with the population it is a subset of. A violation here
        // would render a meter fuller than 100% — i.e. a false statement.
        var pairs = new (string Kpi, string Total)[]
        {
            ("openNcs", "nonconformances"),
            ("overdueCapaActions", "capaActions"),
            ("openComplaints", "complaints"),
            ("auditsInProgress", "audits"),
            ("equipmentOutOfService", "equipmentItems"),
            ("equipmentNeedsCalibration", "equipmentItems"),
            ("highResidualRisks", "risks"),
            ("overdueTasks", "workTasks"),
            ("ptUnsatisfactory", "ptEnrollments"),
            ("pendingTrainingAssignments", "trainingAssignments"),
            ("suspendedSuppliers", "suppliers"),
            ("publishedDocuments", "documents"),
        };

        foreach (var (kpi, total) in pairs)
        {
            var part = kpis.GetProperty(kpi).GetInt32();
            var whole = totals.GetProperty(total).GetInt32();
            part.Should().BeLessThanOrEqualTo(whole,
                $"'{kpi}' is a subset of '{total}', so metering it against that total must never exceed 100%");
        }
    }

    [Fact]
    public async Task A_fresh_tenant_reports_zeroes_rather_than_omitting_the_totals()
    {
        // A brand-new laboratory has no rows at all. The totals must still be
        // present and zero — the tile then shows an honest count with no meter,
        // instead of the UI having to guess.
        var kpis = await KpisAsync(await TenantAdminAsync());
        var totals = kpis.GetProperty("totals");

        totals.GetProperty("nonconformances").GetInt32().Should().Be(0);
        totals.GetProperty("complaints").GetInt32().Should().Be(0);
        kpis.GetProperty("openNcs").GetInt32().Should().Be(0);
    }
}
