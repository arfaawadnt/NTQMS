namespace NT.QAMS.Contracts.Tenancy;

/// <summary>
/// Request to provision a new tenant (control-plane operation), including the
/// tenant administrator's initial credentials — created atomically with the tenant.
/// </summary>
public sealed record ProvisionTenantRequest(
    string Identifier, string Name, string AdminEmail, string AdminDisplayName, string AdminPassword);

/// <summary>Tenant as exposed by the control-plane API.</summary>
public sealed record TenantDto(
    Guid Id,
    string Identifier,
    string Name,
    string Status,
    DateTimeOffset CreatedAtUtc);
