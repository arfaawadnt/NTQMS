namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// The tenant resolved for the current request/operation — from the JWT
/// tenant_id claim ONLY (never headers or query strings). Background jobs
/// set it explicitly per unit of work.
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
    bool IsResolved { get; }

    /// <summary>
    /// True when this unit of work is a trusted cross-tenant operation
    /// (platform provisioning, background sweeps, the outbox processor) that is
    /// deliberately allowed to bypass Row-Level Security. Never set on a normal
    /// request path — the connection sets <c>app.bypass_rls</c> from this flag.
    /// </summary>
    bool IsElevated { get; }
}

/// <summary>Write side used by the request middleware / job scope initializer.</summary>
public interface ICurrentTenantSetter
{
    void Set(Guid tenantId);
    void Clear();

    /// <summary>
    /// Deliberately elevate the current unit of work to cross-tenant (RLS-bypass)
    /// access. Reserved for trusted infrastructure: tenant provisioning, the
    /// outbox processor, and scheduled/KPI sweeps. Must never be called on a
    /// request handling end-user input.
    /// </summary>
    void Elevate();
}
