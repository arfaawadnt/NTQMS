using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// API-003/SEC-003: the framework's default authorization results are BARE
/// status codes — a role-gate 403 or a challenge 401 carried no body at all,
/// breaking the "every error is problem+json with a stable code" contract.
/// This handler keeps the default challenge semantics (WWW-Authenticate) but
/// writes the standard problem body for both outcomes.
/// </summary>
public sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    /// <summary>Stable code for an HTTP-layer role-gate refusal.</summary>
    public const string ForbiddenCode = "AUTHZ-403";

    /// <summary>Stable code for a missing/invalid credential challenge.</summary>
    public const string ChallengedCode = "AUTH-401";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            await ProblemResponse.WriteAsync(
                context, StatusCodes.Status403Forbidden,
                "You do not have permission to perform this action.", ForbiddenCode);
            return;
        }

        if (authorizeResult.Challenged)
        {
            // Let the scheme set its 401 + WWW-Authenticate first, then attach
            // the problem body the contract promises.
            await _default.HandleAsync(next, context, policy, authorizeResult);
            if (!context.Response.HasStarted
                && context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                await ProblemResponse.WriteAsync(
                    context, StatusCodes.Status401Unauthorized,
                    "Authentication is required.", ChallengedCode);
            }

            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
