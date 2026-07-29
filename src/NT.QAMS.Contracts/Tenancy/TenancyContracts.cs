namespace NT.QAMS.Contracts.Tenancy;

/// <summary>
/// Request to provision a new tenant (control-plane operation), including the
/// tenant administrator's initial credentials — created atomically with the tenant.
/// </summary>
public sealed record ProvisionTenantRequest(
    string Identifier, string Name, string AdminEmail, string AdminDisplayName, string AdminPassword);

/// <summary>Per-tenant security policy: whether privileged users must enrol MFA (F-04).</summary>
public sealed record TenantMfaPolicyDto(bool RequireMfaForPrivilegedRoles);

/// <summary>Set the current tenant's privileged-MFA enforcement.</summary>
public sealed record SetTenantMfaPolicyRequest(bool Require);

/// <summary>Tenant as exposed by the control-plane API.</summary>
public sealed record TenantDto(
    Guid Id,
    string Identifier,
    string Name,
    string Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Public, pre-authentication branding for a laboratory's own sign-in address.
/// Carries the display NAME only — never ids, status or settings.
/// </summary>
public sealed record WorkspaceResponse(string Name);
