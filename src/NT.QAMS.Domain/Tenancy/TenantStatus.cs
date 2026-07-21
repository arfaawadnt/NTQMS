namespace NT.QAMS.Domain.Tenancy;

/// <summary>
/// Tenant lifecycle per the domain model: Provisioning → Active → Suspended → Terminated.
/// Typed enum, never a magic string — persisted as text via EF conversion.
/// </summary>
public enum TenantStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    Terminated = 3,
}
