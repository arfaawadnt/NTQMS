using System.Diagnostics;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// OBS-001/OBS-002: correlation + the canonical request-completion log.
/// <list type="bullet">
/// <item>Accepts a caller-supplied <c>X-Correlation-Id</c> (sanitized), else
/// uses the trace id; echoes it on every response so a client error report
/// can always be joined to server logs and the trace.</item>
/// <item>Opens a log scope (service, environment, correlation, trace) for
/// everything the request logs.</item>
/// <item>Emits ONE structured completion record per request with the
/// standard fields: service, environment, tenant, user, operation, status,
/// outcome, duration — the log-shape contract asserted by test.</item>
/// </list>
/// Sits first in the pipeline so even failures and 401s are covered.
/// </summary>
public sealed partial class ObservabilityMiddleware(
    RequestDelegate next,
    IHostEnvironment environment,
    ILogger<ObservabilityMiddleware> logger)
{
    /// <summary>Correlation header, inbound (optional) and outbound (always).</summary>
    public const string CorrelationHeaderName = "X-Correlation-Id";

    /// <summary>HttpContext.Items key carrying the resolved correlation id.</summary>
    public const string CorrelationItemKey = "qams.correlation_id";

    /// <summary>Logical service name stamped on every log record and trace resource.</summary>
    public const string ServiceName = "nt-qams-api";

    private const int MaxCorrelationIdLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var started = Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Service"] = ServiceName,
            ["Environment"] = environment.EnvironmentName,
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString(),
        });

        try
        {
            await next(context);
        }
        finally
        {
            // Resolved AFTER the pipeline so authentication/tenant middleware
            // have populated the scoped request identity.
            var tenant = context.RequestServices.GetService<ICurrentTenant>()?.TenantId;
            var user = context.RequestServices.GetService<ICurrentUser>()?.UserId;
            var status = context.Response.StatusCode;

            LogRequestCompleted(
                logger,
                ServiceName,
                environment.EnvironmentName,
                context.Request.Method,
                context.Request.Path,
                context.GetEndpoint()?.DisplayName ?? $"{context.Request.Method} {context.Request.Path}",
                status,
                status >= StatusCodes.Status500InternalServerError ? "server-error"
                    : status >= StatusCodes.Status400BadRequest ? "client-error"
                    : "success",
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                tenant,
                user,
                correlationId);
        }
    }

    /// <summary>Caller-supplied id (sanitized) or the current trace id.</summary>
    private static string ResolveCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[CorrelationHeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(supplied)
            && supplied.Length <= MaxCorrelationIdLength
            && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            return supplied;
        }

        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Service} {Environment} {Method} {Path} → {Status} {Outcome} in {DurationMs:0.0}ms " +
                  "op={Operation} tenant={TenantId} user={UserId} correlation={CorrelationId}")]
    private static partial void LogRequestCompleted(
        ILogger logger, string service, string environment, string method, string path,
        string operation, int status, string outcome, double durationMs,
        Guid? tenantId, Guid? userId, string correlationId);
}
