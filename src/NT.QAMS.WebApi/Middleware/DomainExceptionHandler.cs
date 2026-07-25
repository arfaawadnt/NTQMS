using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// Maps domain and validation failures to RFC 7807 problem details.
/// Domain errors carry their machine-readable code (e.g. "TENANT-005",
/// later "SOD-CAPA-001") in the extensions.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
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
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
