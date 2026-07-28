using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-6 finding SEC-003 over the real pipeline: the role×endpoint deny
/// matrix for the read-only ExternalAuditor. Reads succeed; every write
/// command — including the analytical/NC mutations the audit flagged as
/// previously ungated — is refused with 403 + AUTHZ-002 by the
/// application-layer AuthorizationBehavior, independent of controller
/// attributes.
/// </summary>
public sealed class AuditorDenyMatrixTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthResponse(string accessToken);

    private async Task<HttpClient> AuditorClientAsync()
    {
        var platform = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });
        var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);

        var slug = $"aud-lab-{Guid.NewGuid():N}"[..20];
        (await admin.PostAsJsonAsync("/api/tenants", new
        {
            identifier = slug,
            name = "Auditor Lab",
            adminEmail = $"qa@{slug}.test",
            adminDisplayName = "QA",
            adminPassword = "Aud-Lab-Pass-1!",
        })).EnsureSuccessStatusCode();

        var tenantLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"qa@{slug}.test",
            password = "Aud-Lab-Pass-1!",
        });
        var tenantAdmin = factory.CreateClient();
        tenantAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);

        (await tenantAdmin.PostAsJsonAsync("/api/users", new
        {
            email = $"auditor@{slug}.test",
            displayName = "External Auditor",
            role = "ExternalAuditor",
            initialPassword = "Auditor-Pass-1!",
        })).EnsureSuccessStatusCode();

        var auditorLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantIdentifier = slug,
            email = $"auditor@{slug}.test",
            password = "Auditor-Pass-1!",
        });
        var auditor = factory.CreateClient();
        auditor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await auditorLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken);
        return auditor;
    }

    [Fact]
    public async Task The_auditor_reads_the_quality_ledger_but_every_write_is_403()
    {
        var auditor = await AuditorClientAsync();

        // READ access — the auditor's entire purpose.
        (await auditor.GetAsync("/api/nonconformances")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await auditor.GetAsync("/api/documents")).StatusCode.Should().Be(HttpStatusCode.OK);

        // WRITE matrix — the previously-ungated mutations the audit flagged.
        var writes = new (string Name, Func<Task<HttpResponseMessage>> Attempt)[]
        {
            ("raise NC", () => auditor.PostAsJsonAsync("/api/nonconformances", new
            {
                title = "auditor should not do this",
                description = "x",
                severity = 3,
                likelihood = 2,
                sourceType = "Internal",
            })),
            ("configure outlier screening", () => auditor.PostAsJsonAsync("/api/outlier-screenings", new
            {
                screeningRef = "OUT-X",
                dataset = "d",
                unit = "u",
            })),
            ("create document", () => auditor.PostAsJsonAsync("/api/documents", new
            {
                code = "SOP-X",
                title = "t",
                category = "SOP",
                fileId = Guid.NewGuid(),
                changeSummary = "s",
                reviewCycleMonths = 12,
            })),
        };

        foreach (var (name, attempt) in writes)
        {
            var response = await attempt();
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                $"SEC-003: the read-only auditor must never execute '{name}'");

            // Denied either by a controller role gate (AUTHZ-403) or by the
            // application-layer behavior (AUTHZ-002) — both speak problem+json.
            var raw = await response.Content.ReadAsStringAsync();
            raw.Should().NotBeNullOrWhiteSpace($"'{name}' must carry the problem body (API-003)");
            using var body = JsonDocument.Parse(raw);
            body.RootElement.GetProperty("code").GetString().Should().StartWith("AUTHZ-");
        }
    }
}
