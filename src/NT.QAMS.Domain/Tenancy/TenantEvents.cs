using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Tenancy;

/// <summary>
/// Tenancy domain events. Consumers (per the event routing table):
/// TenantProvisioned → Identity seeds the tenant admin + canonical roles,
/// Organization seeds default LOVs; TenantSuspended/Reactivated gate access.
/// </summary>
public sealed record TenantProvisioned(Guid TenantId, string Slug, string Name) : DomainEvent;

public sealed record TenantSuspended(Guid TenantId, string Slug, string Reason) : DomainEvent;

public sealed record TenantReactivated(Guid TenantId, string Slug) : DomainEvent;

public sealed record TenantTerminated(Guid TenantId, string Slug) : DomainEvent;
