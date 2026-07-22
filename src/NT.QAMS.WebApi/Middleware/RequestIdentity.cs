using System.Security.Claims;
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
