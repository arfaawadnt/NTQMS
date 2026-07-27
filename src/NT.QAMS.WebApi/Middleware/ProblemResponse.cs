using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// API-003: the ONE writer for error bodies. Every failure path — the domain
/// exception handler and the auth/tenant/change-reason/MFA middlewares — emits
/// the same RFC 7807 shape with the same media type
/// (<c>application/problem+json</c>), a stable machine-readable <c>code</c>,
/// and the trace/correlation ids that join the response to server logs.
/// Anonymous-object shapes are banned: contract drift between error paths is
/// exactly what this kills.
/// </summary>
public static class ProblemResponse
{
    /// <summary>RFC 7807 media type.</summary>
    public const string ContentType = "application/problem+json";

    /// <summary>Writes <paramref name="problem"/> with ids + problem media type.</summary>
    public static async Task WriteAsync(
        HttpContext httpContext, ProblemDetails problem, CancellationToken cancellationToken = default)
    {
        problem.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        if (httpContext.Items[ObservabilityMiddleware.CorrelationItemKey] is string correlationId)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        httpContext.Response.StatusCode =
            problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problem, options: null, contentType: ContentType, cancellationToken);
    }

    /// <summary>Convenience for the middlewares: title + status + stable code.</summary>
    public static Task WriteAsync(
        HttpContext httpContext, int status, string title, string code,
        CancellationToken cancellationToken = default) =>
        WriteAsync(httpContext, new ProblemDetails
        {
            Status = status,
            Title = title,
            Extensions = { ["code"] = code },
        }, cancellationToken);
}
