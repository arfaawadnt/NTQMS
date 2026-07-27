using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NT.QAMS.WebApi.Middleware;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-4 finding API-003: EVERY error path — middleware short-circuits and
/// the domain exception handler alike — answers with the same RFC 7807
/// contract: media type <c>application/problem+json</c>, a stable
/// machine-readable <c>code</c>, and the trace id. No anonymous-object drift.
/// </summary>
public sealed class ProblemContractTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json",
            "API-003: every error path uses the one problem writer");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be(code);
        body.RootElement.GetProperty("status").GetInt32().Should().Be((int)status);
        body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Change_reason_middleware_speaks_problem_json()
    {
        var response = await _client.DeleteAsync("/api/lovs/does-not-matter");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "CHANGE-REASON-REQUIRED");
    }

    [Fact]
    public async Task Domain_exception_handler_speaks_problem_json()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = "definitely-wrong-password-1!",
        });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "AUTH-001");
    }
}
