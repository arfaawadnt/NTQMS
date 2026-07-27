using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NT.QAMS.WebApi.Security;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests
{
    /// <summary>
    /// Phase-4 finding API-005 over the real pipeline: only allow-listed
    /// evidence types upload, a renamed binary fails the magic-byte sniff,
    /// the stored content type is the canonical one (never the client's
    /// claim), and downloads are forced to attachment.
    /// </summary>
    public sealed class FileHardeningTests(QamsWebAppFactory factory)
        : IClassFixture<QamsWebAppFactory>
    {
        private readonly HttpClient _client = factory.CreateClient();

        private sealed record AuthResponse(string accessToken);
        private sealed record UploadedResponse(Guid id, string fileName);

        private async Task<HttpClient> TenantClientAsync()
        {
            var platform = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = QamsWebAppFactory.PlatformAdminEmail,
                password = QamsWebAppFactory.PlatformAdminPassword,
            });
            var platformToken = (await platform.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);
            var slug = $"file-lab-{Guid.NewGuid():N}"[..20];
            (await client.PostAsJsonAsync("/api/tenants", new
            {
                identifier = slug,
                name = "File Lab",
                adminEmail = $"qa@{slug}.test",
                adminDisplayName = "QA",
                adminPassword = "File-Lab-Pass-1!",
            })).EnsureSuccessStatusCode();

            var tenantLogin = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                tenantIdentifier = slug,
                email = $"qa@{slug}.test",
                password = "File-Lab-Pass-1!",
            });
            var tenantToken = (await tenantLogin.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken;

            var tenantClient = factory.CreateClient();
            tenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
            return tenantClient;
        }

        private static MultipartFormDataContent Form(string fileName, string contentType, byte[] bytes)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return new MultipartFormDataContent { { content, "file", fileName } };
        }

        private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        private static byte[] ExecutableBytes() => [0x4D, 0x5A, 0x90, 0x00, 3, 0, 0, 0]; // "MZ"

        [Fact]
        public async Task A_renamed_executable_fails_the_magic_byte_sniff()
        {
            var client = await TenantClientAsync();

            var response = await client.PostAsync(
                "/api/files", Form("report.pdf", "application/pdf", ExecutableBytes()));

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await response.Content.ReadAsStringAsync()).Should().Contain("FILE-415");
        }

        [Fact]
        public async Task A_disallowed_extension_is_rejected()
        {
            var client = await TenantClientAsync();

            var response = await client.PostAsync(
                "/api/files", Form("payload.exe", "application/octet-stream", ExecutableBytes()));

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await response.Content.ReadAsStringAsync()).Should().Contain("FILE-415");
        }

        [Fact]
        public async Task A_valid_upload_stores_the_canonical_type_and_downloads_as_attachment()
        {
            var client = await TenantClientAsync();

            // The client lies about the content type; the sniffed canonical wins.
            var upload = await client.PostAsync(
                "/api/files", Form("evidence.png", "text/html", Png()));
            upload.StatusCode.Should().Be(HttpStatusCode.Created);
            var uploaded = await upload.Content.ReadFromJsonAsync<UploadedResponse>();

            var download = await client.GetAsync($"/api/files/{uploaded!.id}");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            download.Content.Headers.ContentType!.MediaType.Should().Be("image/png",
                "the stored type is the canonical sniffed one, never the client's claim");
            download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment",
                "evidence is downloaded, never rendered in the app origin");
        }

        [Fact]
        public void The_policy_rejects_binary_masquerading_as_text()
        {
            byte[] header = [0x41, 0x42, 0x00, 0x43];

            var (canonical, refusal) = FileContentPolicy.Inspect("data.csv", header);

            canonical.Should().BeNull();
            refusal.Should().Contain("binary");
        }
    }
}
