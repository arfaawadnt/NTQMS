namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// SEC-011/SEC-012: defensive headers on EVERY response (success, error,
/// probe). The API serves JSON only, so its CSP is fully locked down —
/// nothing may load, script, frame, or submit; inline script is blocked by
/// definition (no script-src grant exists). The SPA's own host serves the
/// SPA-appropriate CSP (deploy/web.config). HSTS is emitted outside
/// Development — TLS itself terminates at the reverse proxy (ADR-0002) and
/// browsers ignore HSTS on plain HTTP, so the header is safe everywhere.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    /// <summary>The API's locked-down policy: deny every load/embed/submit vector.</summary>
    public const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>Two years, subdomains included — the SEC-012 HSTS commitment.</summary>
    public const string StrictTransportSecurityValue = "max-age=63072000; includeSubDomains";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (!environment.IsDevelopment())
            {
                headers.StrictTransportSecurity = StrictTransportSecurityValue;
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
