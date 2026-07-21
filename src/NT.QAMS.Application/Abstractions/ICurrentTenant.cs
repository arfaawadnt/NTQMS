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
}

/// <summary>Write side used by the request middleware / job scope initializer.</summary>
public interface ICurrentTenantSetter
{
    void Set(Guid tenantId);
    void Clear();
}
