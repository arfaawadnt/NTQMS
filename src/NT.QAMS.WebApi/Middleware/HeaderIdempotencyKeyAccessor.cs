using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.WebApi.Middleware;

/// <summary>
/// CQRS-004: surfaces the caller's <c>Idempotency-Key</c> header (IETF draft
/// convention) to the application pipeline. Absent or oversized keys read as
/// null — the command simply executes without replay protection.
/// </summary>
public sealed class HeaderIdempotencyKeyAccessor(IHttpContextAccessor accessor) : IIdempotencyKeyAccessor
{
    /// <summary>Request header carrying the client-generated retry key.</summary>
    public const string HeaderName = "Idempotency-Key";

    private const int MaxKeyLength = 100;

    public string? Key
    {
        get
        {
            var raw = accessor.HttpContext?.Request.Headers[HeaderName].ToString();
            return string.IsNullOrWhiteSpace(raw) || raw.Length > MaxKeyLength ? null : raw;
        }
    }
}
