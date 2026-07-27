using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Services;

/// <summary>
/// Scoped holder for the resolved tenant. Written once per request by the
/// tenant-resolution middleware (from the JWT claim only) or per unit of work
/// by background jobs. Transaction-scoped by DI lifetime — never leaks across
/// pooled requests.
/// </summary>
public sealed class CurrentTenant : ICurrentTenant, ICurrentTenantSetter
{
    public Guid? TenantId { get; private set; }
    public bool IsResolved => TenantId.HasValue;
    public bool IsElevated { get; private set; }

    public void Set(Guid tenantId) => TenantId = tenantId;

    public void Clear()
    {
        TenantId = null;
        IsElevated = false;
    }

    public void Elevate() => IsElevated = true;
}

/// <summary>
/// Scoped holder for the current unit of work's change reason. Written once per
/// request by the change-reason middleware (from the <c>X-Change-Reason</c>
/// header) and read by the field-change interceptor when it writes the ledger.
/// Transaction-scoped by DI lifetime — never leaks across pooled requests.
/// </summary>
public sealed class CurrentChangeReason : ICurrentChangeReason, ICurrentChangeReasonSetter
{
    public string? Reason { get; private set; }

    public void Set(string? reason) =>
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}

/// <summary>
/// Placeholder actor until Identity &amp; Access (Phase 1) supplies the real
/// JWT-backed implementation. Unauthenticated by design — nothing in Phase 0
/// grants privileges based on it.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
    public string? DisplayName => null;
    public bool IsAuthenticated => false;
    public NT.QAMS.Domain.IdentityAccess.UserRole? Role => null;
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
