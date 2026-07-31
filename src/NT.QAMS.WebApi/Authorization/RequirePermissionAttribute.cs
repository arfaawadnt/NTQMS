using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Middleware;

namespace NT.QAMS.WebApi.Authorization;

/// <summary>
/// Requires a configurable privilege on the action, e.g.
/// <c>[RequirePermission(PermissionCatalog.Nonconformances, PermissionAction.Approve)]</c>.
/// <para>
/// This is the HTTP gate that replaced <c>[Authorize(Roles = …)]</c>. Roles are
/// tenant data now, so the endpoint can no longer name the roles that reach it —
/// it names the capability it represents, and whichever roles the laboratory has
/// granted that capability are the roles that get through.
/// </para>
/// <para>
/// Authentication is still <c>[Authorize]</c>'s job and runs first, so an
/// anonymous caller sees 401 here rather than 403. A caller who is authenticated
/// but unprivileged gets 403 with code <c>AUTHZ-403</c> — the same code and the
/// same problem+json shape the framework handler emits, so the SPA has one path
/// to handle.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>Gates the action on <paramref name="module"/> + <paramref name="action"/>.</summary>
    public RequirePermissionAttribute(string module, PermissionAction action)
    {
        PermissionKey = PermissionCatalog.Key(module, action);
    }

    /// <summary>The <c>{module}.{action}</c> key this action requires.</summary>
    public string PermissionKey { get; }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Unauthenticated requests are the authentication middleware's business;
        // reaching here without an identity means the action is [AllowAnonymous]
        // and gating it on a privilege would be a programming error, not a denial.
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var privileges = context.HttpContext.RequestServices.GetRequiredService<IUserPrivileges>();
        if (privileges.Has(PermissionKey))
        {
            return Task.CompletedTask;
        }

        context.Result = new EmptyResult();
        return ProblemResponse.WriteAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "You do not have permission to perform this action.",
            ProblemAuthorizationResultHandler.ForbiddenCode);
    }
}
