namespace NT.QAMS.SharedKernel.MultiTenancy;

/// <summary>
/// Marks an aggregate/entity as belonging to exactly one tenant.
/// TenantId is stamped by the persistence interceptor from the resolved request
/// tenant — domain and application code never set it — and is enforced by
/// (1) the EF global query filter, (2) PostgreSQL row-level security,
/// (3) composite tenant-aware foreign keys.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
