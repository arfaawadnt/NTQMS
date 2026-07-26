using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// A mutable request-context stand-in that drives the real interceptors from a
/// test: it plays ICurrentTenant/Setter (so TenantConnectionInterceptor stamps
/// the tenant GUC and TenantStampInterceptor stamps rows) and ICurrentUser (for
/// the audit / field-change ledgers).
/// </summary>
public sealed class TestContext
    : ICurrentTenant, ICurrentTenantSetter, ICurrentUser, ICurrentChangeReason, ICurrentChangeReasonSetter
{
    public Guid? TenantId { get; private set; }
    public bool IsResolved => TenantId.HasValue;
    public bool IsElevated { get; private set; }

    public Guid? UserId { get; set; } = Guid.CreateVersion7();
    public string? DisplayName { get; set; } = "integration-test";
    public bool IsAuthenticated => true;

    public string? Reason { get; private set; }

    public void Set(Guid tenantId) => TenantId = tenantId;
    public void Clear() { TenantId = null; IsElevated = false; }
    public void Elevate() => IsElevated = true;
    public void Set(string? reason) => Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}

/// <summary>Fixed clock — record timestamps stay deterministic in tests.</summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
}
