using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// Maps domain and validation failures to RFC 7807 problem details.
/// Domain errors carry their machine-readable code (e.g. "TENANT-005",
/// later "SOD-CAPA-001") in the extensions.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// Stable code for an optimistic-concurrency conflict (DB-009/VAL-003):
    /// the row's xmin changed between read and write — the client reloads and
    /// reapplies its change.
    /// </summary>
    public const string ConcurrencyConflictCode = "CONCURRENCY-409";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The record was modified by someone else since it was loaded — reload and retry.",
                Extensions = { ["code"] = ConcurrencyConflictCode },
            },
            ValidationException validation => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed.",
                Extensions =
                {
                    ["errors"] = validation.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                },
            },
            InvalidStateTransitionException transition => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = transition.Message,
                Extensions = { ["code"] = transition.Code },
            },
            // Exact "AUTH-" prefix: authentication failures only. Authorization-matrix
            // codes (AUTHZ-*) are business errors and must NOT masquerade as 401s —
            // the SPA treats 401 as a session problem.
            DomainException auth when auth.Code.StartsWith("AUTH-", StringComparison.Ordinal) => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = auth.Message,
                Extensions = { ["code"] = auth.Code },
            },
            // SEC-003: application-layer authorization refusals (AUTHZ-*) are
            // 403 Forbidden — an authenticated actor lacking permission is
            // neither a session problem (401) nor a business-rule failure (422).
            DomainException authz when authz.Code.StartsWith("AUTHZ-", StringComparison.Ordinal) => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = authz.Message,
                Extensions = { ["code"] = authz.Code },
            },
            DomainException notFound when notFound.Code.EndsWith("-404", StringComparison.Ordinal) => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = notFound.Message,
                Extensions = { ["code"] = notFound.Code },
            },
            DomainException domain => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = domain.Message,
                Extensions = { ["code"] = domain.Code },
            },
            // M-11: boundary conversions (RequestEnum, Guid/date parsing) throw
            // ArgumentException for malformed request input — the client's
            // error, surfaced as a 400 problem instead of an unhandled 500. The
            // message is the parse diagnostic ("'X' is not a valid HarmGrade."),
            // which identifies the offending field without leaking internals.
            ArgumentException argument => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = argument.Message,
                Extensions = { ["code"] = "REQ-001" },
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        // API-003/OBS-002: the shared writer stamps trace/correlation ids and
        // the application/problem+json media type on every error path.
        await ProblemResponse.WriteAsync(httpContext, problem, cancellationToken);
        return true;
    }
}
