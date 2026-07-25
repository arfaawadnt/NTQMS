using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Locks down the SOW data-import requirement: an analyzer/LIS CSV import must
/// perform per-row data-integrity checks — valid rows import, invalid rows are
/// rejected with a reason, and the batch commits partially. Exercised over the
/// real method-comparison bulk endpoint.
/// </summary>
public sealed class BulkImportTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private sealed record AuthResponse(string accessToken);
    private sealed record IdResponse(Guid id);
    private sealed record BulkReject(int row, string reason);
    private sealed record BulkResult(int imported, List<BulkReject> rejected);
    private sealed record Detail(int? pairCount, List<object> pairs);

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Csv_import_accepts_valid_rows_and_rejects_invalid_ones_with_reasons()
    {
        var platform = factory.CreateClient();
        var login = await platform.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        Authorize(platform, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        var slug = $"imp-{Guid.NewGuid():N}"[..12];
        (await platform.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Import Lab",
            adminEmail = "imp-admin@functional.test",
            adminDisplayName = "Import Admin",
            adminPassword = "Tenant-Admin-Pass-1!",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var client = factory.CreateClient();
        var tenantLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = "imp-admin@functional.test",
            password = "Tenant-Admin-Pass-1!",
        });
        Authorize(client, (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        var create = await client.PostAsJsonAsync("/api/method-comparisons", new
        {
            analyte = "Glucose", unit = "mmol/L", referenceMethod = "Cobas", testMethod = "Architect",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var studyId = (await create.Content.ReadFromJsonAsync<IdResponse>())!.id;

        // Three valid rows + two invalid (a zero and a negative reference value).
        var import = await client.PostAsJsonAsync($"/api/method-comparisons/{studyId}/pairs/import", new
        {
            rows = new object[]
            {
                new { referenceValue = 5.1m, testValue = 5.3m, sampleId = "P1" },
                new { referenceValue = 8.0m, testValue = 8.2m, sampleId = "P2" },
                new { referenceValue = 0m, testValue = 4.0m, sampleId = "BAD-ZERO" },
                new { referenceValue = 12.0m, testValue = 12.4m, sampleId = "P3" },
                new { referenceValue = -1m, testValue = 4.0m, sampleId = "BAD-NEG" },
            },
        });
        import.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await import.Content.ReadFromJsonAsync<BulkResult>();

        result!.imported.Should().Be(3);
        result.rejected.Should().HaveCount(2);
        result.rejected.Select(r => r.row).Should().BeEquivalentTo(new[] { 3, 5 });
        result.rejected.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.reason));

        var detail = await client.GetFromJsonAsync<Detail>($"/api/method-comparisons/{studyId}");
        detail!.pairs.Should().HaveCount(3, "only the valid rows persist");
    }
}
