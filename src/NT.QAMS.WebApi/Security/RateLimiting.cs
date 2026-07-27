using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace NT.QAMS.WebApi.Security;

/// <summary>Typed rate-limit configuration (RateLimit:* keys), validated at composition.</summary>
/// <param name="GlobalPermitPerMinute">Per-client budget for the whole API surface.</param>
/// <param name="AuthPermitPerMinute">Per-client budget for /api/auth/* (credential guessing).</param>
/// <param name="ESignaturePermitPerMinute">Per-actor budget for password+PIN signing ceremonies (PIN guessing).</param>
public sealed record RateLimitSettings(
    int GlobalPermitPerMinute,
    int AuthPermitPerMinute,
    int ESignaturePermitPerMinute)
{
    public RateLimitSettings Validated() =>
        GlobalPermitPerMinute > 0 && AuthPermitPerMinute > 0 && ESignaturePermitPerMinute > 0
            ? this
            : throw new InvalidOperationException("RateLimit:* permits must all be positive.");

    public static RateLimitSettings From(IConfiguration configuration) => new(
        int.TryParse(configuration["RateLimit:GlobalPermitPerMinute"], out var global) ? global : 300,
        int.TryParse(configuration["RateLimit:AuthPermitPerMinute"], out var auth) ? auth : 10,
        int.TryParse(configuration["RateLimit:ESignaturePermitPerMinute"], out var esign) ? esign : 10);
}

/// <summary>
/// SEC-013/API-002: request throttling. A global per-client window protects
/// the whole surface; stricter partitions guard the credential endpoints
/// (/api/auth/*) and the password+PIN e-signature ceremonies, where a burst is
/// an attack (credential/PIN guessing), not a workload. Rejections are 429
/// with Retry-After. Health/metrics probes are exempted at the endpoint.
/// </summary>
public static class RateLimiting
{
    /// <summary>Policy for credential endpoints — applied on AuthController.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Policy for password+PIN signing ceremonies — applied per signing endpoint.</summary>
    public const string ESignaturePolicy = "esignature";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static void Configure(RateLimiterOptions options, RateLimitSettings settings)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Window.TotalSeconds).ToString();
            return ValueTask.CompletedTask;
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.GlobalPermitPerMinute,
                Window = Window,
                QueueLimit = 0,
            }));

        options.AddPolicy(AuthPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.AuthPermitPerMinute,
                Window = Window,
                QueueLimit = 0,
            }));

        options.AddPolicy(ESignaturePolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(ActorKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.ESignaturePermitPerMinute,
                Window = Window,
                QueueLimit = 0,
            }));
    }

    /// <summary>Client identity: source address (real client IP once the proxy's X-Forwarded-For is applied).</summary>
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Signing ceremonies are authenticated — throttle the ACTOR, not the address.</summary>
    private static string ActorKey(HttpContext context) =>
        context.User.FindFirst("sub")?.Value ?? ClientKey(context);
}
