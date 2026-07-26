using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Tenancy;

/// <summary>
/// Control-plane aggregate: a customer laboratory. NOT tenant-scoped (it *is*
/// the tenant), lives in the saas schema outside RLS.
/// Lifecycle: Provisioning → Active → Suspended ⇄ Active → Terminated.
/// State changes only through the guarded transition methods below.
/// </summary>
public sealed class Tenant : AggregateRoot
{
    public const int MaxNameLength = 200;

    private Tenant()
    {
        // EF Core materialization only.
        Slug = null!;
        Name = null!;
        Settings = null!;
    }

    private Tenant(TenantSlug slug, string name)
    {
        Slug = slug;
        Name = name;
        Status = TenantStatus.Active;
        Settings = TenantSettings.Default;
    }

    public TenantSlug Slug { get; private set; }
    public string Name { get; private set; }
    public TenantStatus Status { get; private set; }
    public TenantSettings Settings { get; private set; }
    public string? SuspensionReason { get; private set; }

    /// <summary>
    /// Factory for a provisioned tenant. Slug uniqueness is enforced by the
    /// unique index; the provisioning orchestrator owns the end-to-end saga.
    /// </summary>
    public static Tenant Provision(TenantSlug slug, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("TENANT-003", "Tenant name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new DomainException("TENANT-004", $"Tenant name must not exceed {MaxNameLength} characters.");
        }

        var tenant = new Tenant(slug, name.Trim());
        tenant.Raise(new TenantProvisioned(tenant.Id, slug.Value, tenant.Name));
        return tenant;
    }

    public void Suspend(string reason)
    {
        if (Status != TenantStatus.Active)
        {
            throw new InvalidStateTransitionException(
                "TENANT-010", $"Only an Active tenant can be suspended (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("TENANT-011", "A suspension reason is required.");
        }

        Status = TenantStatus.Suspended;
        SuspensionReason = reason.Trim();
        Raise(new TenantSuspended(Id, Slug.Value, SuspensionReason));
    }

    public void Reactivate()
    {
        if (Status != TenantStatus.Suspended)
        {
            throw new InvalidStateTransitionException(
                "TENANT-012", $"Only a Suspended tenant can be reactivated (current: {Status}).");
        }

        Status = TenantStatus.Active;
        SuspensionReason = null;
        Raise(new TenantReactivated(Id, Slug.Value));
    }

    public void Terminate()
    {
        if (Status == TenantStatus.Terminated)
        {
            throw new InvalidStateTransitionException("TENANT-013", "Tenant is already terminated.");
        }

        Status = TenantStatus.Terminated;
        Raise(new TenantTerminated(Id, Slug.Value));
    }

    public void UpdateSettings(TenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
    }

    /// <summary>Opt this tenant in or out of enforced MFA for its privileged users (F-04).</summary>
    public void SetPrivilegedMfaPolicy(bool require) =>
        Settings = Settings with { RequireMfaForPrivilegedRoles = require };
}
