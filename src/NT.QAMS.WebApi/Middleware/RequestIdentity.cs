using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// Actor from the validated JWT — the only identity source for handlers and audit
/// stamps. The token issues the standard "sub"/"name" claims; we read those first
/// and fall back to the ClaimTypes.* URIs in case JWT inbound-claim remapping is
/// enabled by the host. Reading only NameIdentifier was the v1.0 deployment bug:
/// with remapping off, "sub" never became NameIdentifier and every actor-scoped
/// handler (raise NC, my-notifications) failed.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            var raw = user?.FindFirstValue("sub") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? DisplayName
    {
        get
        {
            var user = accessor.HttpContext?.User;
            return user?.FindFirstValue("name") ?? user?.FindFirstValue(ClaimTypes.Name);
        }
    }

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}

/// <summary>
/// Resolves the request tenant from the validated JWT's tenant_id claim ONLY —
/// never from headers or query strings (the as-built system's spoofable path,
/// explicitly banned by the architecture).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantSetter tenantSetter)
    {
        var claim = context.User.FindFirstValue("tenant_id");
        if (Guid.TryParse(claim, out var tenantId))
        {
            tenantSetter.Set(tenantId);
        }

        await next(context);
    }
}

/// <summary>
/// F-07: server-side session revocation. On every authenticated request the
/// user is re-checked against the database, so a deactivated account — or one
/// whose role changed — stops working immediately instead of lingering until the
/// JWT expires. (Idle timeout is handled client-side by the 30-minute watch.)
/// </summary>
public sealed class ActiveSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(sub, out var userId))
            {
                var row = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, u.Role })
                    .FirstOrDefaultAsync(context.RequestAborted);

                if (row is null || !row.IsActive)
                {
                    await Deny(context, "AUTH-006", "Your session is no longer valid. Please sign in again.");
                    return;
                }

                var tokenRole = context.User.FindFirstValue(ClaimTypes.Role);
                if (!string.Equals(tokenRole, row.Role.ToString(), StringComparison.Ordinal))
                {
                    await Deny(context, "AUTH-007", "Your permissions have changed. Please sign in again.");
                    return;
                }
            }
        }

        await next(context);
    }

    private static async Task Deny(HttpContext context, string code, string title)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { title, status = 401, code });
    }
}

/// <summary>
/// F-04 enforcement: a session issued to a privileged user who has not enrolled
/// MFA carries scope=mfa_enrollment. Such a session may reach ONLY the MFA-
/// enrollment endpoints; every other request is refused with 403 + code
/// MFA-ENROLL-REQUIRED so the client routes the user to enrolment. Full sessions
/// (and anonymous requests) pass straight through.
/// </summary>
public sealed class MfaEnrollmentGateMiddleware(RequestDelegate next)
{
    private static readonly string[] Allowed =
    [
        "/api/auth/mfa/enroll",
        "/api/auth/mfa/confirm",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var scope = context.User.FindFirstValue("scope");
        if (scope == "mfa_enrollment")
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var permitted = Allowed.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            if (!permitted)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Multi-factor authentication must be set up before continuing.",
                    status = 403,
                    code = "MFA-ENROLL-REQUIRED",
                });
                return;
            }
        }

        await next(context);
    }
}
