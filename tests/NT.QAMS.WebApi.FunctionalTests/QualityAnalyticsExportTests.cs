using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// URS-130 over the real pipeline: the Quality Analytics report endpoints render a
/// genuine PDF and a genuine XLSX end-to-end (real query → report pack → QuestPDF /
/// ClosedXML → file response), are gated by `reports.export`, and refuse an
/// unauthenticated caller. The report is a Part 11 §11.10(b) copy of the analytics.
/// </summary>
public sealed class QualityAnalyticsExportTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);

    private async Task<HttpClient> TenantAdminClientAsync()
    {
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);

        var slug = $"qa-rep-{Guid.NewGuid():N}"[..18];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Analytics Lab",
            adminEmail = $"admin@{slug}.test",
            adminDisplayName = "Tenant Admin",
            adminPassword = "Qa-Rep-Admin-1!",
        })).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"admin@{slug}.test",
            password = "Qa-Rep-Admin-1!",
        });
        var tenantAdmin = factory.CreateClient();
        tenantAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return tenantAdmin;
    }

    [Fact]
    public async Task Pdf_endpoint_returns_a_genuine_pdf_to_an_authorized_caller()
    {
        var client = await TenantAdminClientAsync();

        var response = await client.GetAsync("/api/exports/quality-analytics.pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Xlsx_endpoint_returns_a_genuine_workbook_to_an_authorized_caller()
    {
        var client = await TenantAdminClientAsync();

        var response = await client.GetAsync("/api/exports/quality-analytics.xlsx");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        bytes[..4].Should().Equal(0x50, 0x4B, 0x03, 0x04);
    }

    [Fact]
    public async Task Unauthenticated_caller_is_refused()
    {
        var response = await _client.GetAsync("/api/exports/quality-analytics.pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
