using MediatR;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.Application.Behaviors;

/// <summary>
/// Structured request logging with tenant/actor context. Failures log the
/// request name and elapsed time — never payload contents (confidential data).
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        try
        {
            var response = await next();
            LogHandled(logger, requestName, currentTenant.TenantId, currentUser.UserId,
                System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            LogFailed(logger, ex, requestName, currentTenant.TenantId, currentUser.UserId);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Handled {RequestName} tenant={TenantId} user={UserId} in {ElapsedMs:0.0}ms")]
    private static partial void LogHandled(
        ILogger logger, string requestName, Guid? tenantId, Guid? userId, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed {RequestName} tenant={TenantId} user={UserId}")]
    private static partial void LogFailed(
        ILogger logger, Exception ex, string requestName, Guid? tenantId, Guid? userId);
}
