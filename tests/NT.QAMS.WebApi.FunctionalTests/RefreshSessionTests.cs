using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Phase-7 finding (ADR-0009) over the real HTTP pipeline: login sets a
/// hardened refresh cookie; refresh rotates it and yields a fresh access
/// token; replaying a rotated cookie is treated as theft (family revoked);
/// logout revokes the family. The access token stays in the body (SPA memory),
/// never in a cookie.
/// </summary>
public sealed class RefreshSessionTests(QamsWebAppFactory factory)
    : IClassFixture<QamsWebAppFactory>
{
    private const string CookieName = "qams_rt";

    private sealed record AuthResponse(string accessToken, string role, bool mfaRequired);

    private static HttpClient Client(QamsWebAppFactory f) =>
        f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false, // drive the cookie by hand so rotation is observable
        });

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
        await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = QamsWebAppFactory.PlatformAdminEmail,
            password = QamsWebAppFactory.PlatformAdminPassword,
        });

    private static string? RefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var header = cookies.FirstOrDefault(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal));
        return header?[(CookieName.Length + 1)..header.IndexOf(';')];
    }

    [Fact]
    public async Task Login_sets_a_hardened_refresh_cookie_and_keeps_the_access_token_in_the_body()
    {
        var client = Client(factory);
        var response = await LoginAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal));
        setCookie.Should().Contain("httponly", "script must never read the refresh token")
            .And.Contain("secure")
            .And.Contain("samesite=strict", "the cookie must be CSRF-inert")
            .And.Contain("path=/api/auth", "the cookie rides only the auth endpoints");

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.accessToken.Should().NotBeNullOrEmpty("the access token lives in the body → SPA memory");
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_issues_a_fresh_access_token()
    {
        var client = Client(factory);
        var first = RefreshCookie(await LoginAsync(client))!;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{CookieName}={first}");
        var refreshed = await client.SendAsync(request);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!.accessToken.Should().NotBeNullOrEmpty();
        RefreshCookie(refreshed).Should().NotBeNull().And.NotBe(first, "every refresh rotates the token");
    }

    [Fact]
    public async Task Replaying_a_rotated_cookie_revokes_the_family()
    {
        var client = Client(factory);
        var first = RefreshCookie(await LoginAsync(client))!;

        // Use it once — legitimate rotation.
        using (var ok = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh"))
        {
            ok.Headers.Add("Cookie", $"{CookieName}={first}");
            var rotated = await client.SendAsync(ok);
            var successor = RefreshCookie(rotated)!;

            // Replay the ORIGINAL (now-rotated) cookie — the theft signal.
            using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            replay.Headers.Add("Cookie", $"{CookieName}={first}");
            (await client.SendAsync(replay)).StatusCode.Should()
                .Be(HttpStatusCode.Unauthorized, "reusing a rotated token is treated as theft");

            // …and the successor is now dead too — the whole family was revoked.
            using var successorTry = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            successorTry.Headers.Add("Cookie", $"{CookieName}={successor}");
            (await client.SendAsync(successorTry)).StatusCode.Should()
                .Be(HttpStatusCode.Unauthorized, "reuse detection revokes the entire family");
        }
    }

    [Fact]
    public async Task Logout_revokes_the_family_and_clears_the_cookie()
    {
        var client = Client(factory);
        var cookie = RefreshCookie(await LoginAsync(client))!;

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"{CookieName}={cookie}");
        var loggedOut = await client.SendAsync(logout);
        loggedOut.StatusCode.Should().Be(HttpStatusCode.NoContent);
        loggedOut.Headers.GetValues("Set-Cookie").Should()
            .Contain(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal), "the cookie is cleared");

        using var afterLogout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        afterLogout.Headers.Add("Cookie", $"{CookieName}={cookie}");
        (await client.SendAsync(afterLogout)).StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized, "a revoked session cannot refresh");
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_unauthorized_not_a_server_error()
    {
        var client = Client(factory);

        var response = await client.PostAsync("/api/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
