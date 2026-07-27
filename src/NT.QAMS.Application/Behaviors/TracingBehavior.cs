using System.Diagnostics;
using MediatR;

namespace NT.QAMS.Application.Behaviors;

/// <summary>The Application layer's tracing source (BCL ActivitySource — no vendor packages).</summary>
public static class ApplicationDiagnostics
{
    /// <summary>Source name the tracer provider subscribes to.</summary>
    public const string ActivitySourceName = "NT.QAMS.Application";

    /// <summary>Spans for MediatR requests (commands/queries).</summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);
}

/// <summary>
/// OBS-002: one tracing span per MediatR request (command/query), nested under
/// the HTTP server span, so a trace reads HTTP → MediatR → EF. The WebApi
/// composition root subscribes OpenTelemetry to
/// <see cref="ApplicationDiagnostics.ActivitySourceName"/>.
/// </summary>
public sealed class TracingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity($"mediatr {typeof(TRequest).Name}");
        activity?.SetTag("qams.request", typeof(TRequest).Name);

        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }
}
