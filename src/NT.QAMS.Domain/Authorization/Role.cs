using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Authorization;

/// <summary>One permission granted to a role. Owned by <see cref="Role"/>.</summary>
public sealed class RolePermission
{
    private RolePermission() { PermissionKey = null!; }

    internal RolePermission(string permissionKey) => PermissionKey = permissionKey;

    /// <summary>A key from <see cref="PermissionCatalog"/>, persisted verbatim.</summary>
    public string PermissionKey { get; private set; }
}

/// <summary>
/// A named set of privileges that users are assigned to. Roles are tenant data,
/// not code: a laboratory composes the roles its own organisation needs, and the
/// system enforces whatever they compose.
/// <para>
/// Two invariants protect the tenant from itself. A <see cref="IsSystem"/> role is
/// seeded and cannot be deleted or renamed (historical records and task queues
/// reference it by name), though its privileges may be tuned. And no role may give
/// away the last remaining grant of <see cref="PermissionCatalog.ManageRoles"/> —
/// enforced where roles are saved, because it spans aggregates.
/// </para>
/// <para>
/// Every change raises a domain event, so privilege changes land in the
/// hash-chained audit trail: under 21 CFR Part 11 §11.10 a change to who may do
/// what is itself a regulated record.
/// </para>
/// </summary>
public sealed class Role : AggregateRoot, ITenantScoped
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
        Name = null!;
        NormalizedName = null!;
    }

    public Guid TenantId { get; set; }

    /// <summary>Display name, unique per tenant (case-insensitively).</summary>
    public string Name { get; private set; }

    /// <summary>Upper-cased name backing the tenant-scoped uniqueness index.</summary>
    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// True for the roles the platform seeds. They may be re-privileged but never
    /// deleted or renamed, so references to them stay resolvable.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    /// Language new members of this role start in. Null means "inherit the
    /// tenant's default"; a user's own preference still wins over both.
    /// </summary>
    public string? DefaultLanguage { get; private set; }

    /// <summary>Deactivated roles cannot be assigned to a user; existing holders keep working until moved.</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyList<RolePermission> Permissions => _permissions;

    /// <summary>The granted permission keys, for authorization decisions.</summary>
    public IEnumerable<string> PermissionKeys => _permissions.Select(p => p.PermissionKey);

    /// <summary>Creates a tenant-defined role.</summary>
    public static Role Create(string name, string? description, IEnumerable<string> permissionKeys, string? defaultLanguage = null)
        => Create(name, description, permissionKeys, isSystem: false, defaultLanguage);

    /// <summary>Creates one of the seeded system roles. Not reachable from the API.</summary>
    public static Role CreateSystem(string name, string? description, IEnumerable<string> permissionKeys)
        => Create(name, description, permissionKeys, isSystem: true, defaultLanguage: null);

    private static Role Create(
        string name, string? description, IEnumerable<string> permissionKeys, bool isSystem, string? defaultLanguage)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("ROLE-001", "A role name is required.");
        }

        if (trimmed.Length > 80)
        {
            throw new DomainException("ROLE-002", "A role name may not exceed 80 characters.");
        }

        var role = new Role
        {
            Name = trimmed,
            NormalizedName = trimmed.ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsSystem = isSystem,
            IsActive = true,
            DefaultLanguage = Normalize(defaultLanguage),
        };

        role.ReplacePermissions(permissionKeys);
        role.Raise(new RoleCreated(role.Id, role.Name, role.IsSystem));
        return role;
    }

    /// <summary>Renames a tenant-defined role. System roles are fixed.</summary>
    public void Rename(string name, string? description)
    {
        if (IsSystem)
        {
            throw new DomainException("ROLE-003", "A system role cannot be renamed.");
        }

        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("ROLE-001", "A role name is required.");
        }

        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Raise(new RoleRenamed(Id, Name));
    }

    /// <summary>
    /// Sets the role's privileges to exactly this set. Unknown keys are rejected
    /// rather than stored: a grant that maps to no code path would read as an
    /// active privilege while doing nothing.
    /// </summary>
    public void SetPermissions(IEnumerable<string> permissionKeys, string reason)
    {
        var before = _permissions.Select(p => p.PermissionKey).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        ReplacePermissions(permissionKeys);
        var after = _permissions.Select(p => p.PermissionKey).OrderBy(k => k, StringComparer.Ordinal).ToArray();

        if (before.SequenceEqual(after, StringComparer.Ordinal))
        {
            return;
        }

        var granted = after.Except(before, StringComparer.Ordinal).ToArray();
        var revoked = before.Except(after, StringComparer.Ordinal).ToArray();
        Raise(new RolePermissionsChanged(Id, Name, granted, revoked, reason));
    }

    /// <summary>Sets the starting language for members of this role; null inherits the tenant default.</summary>
    public void SetDefaultLanguage(string? language) => DefaultLanguage = Normalize(language);

    /// <summary>Stops the role being assigned to further users.</summary>
    public void Deactivate()
    {
        if (IsSystem)
        {
            throw new DomainException("ROLE-004", "A system role cannot be deactivated.");
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Raise(new RoleDeactivated(Id, Name));
    }

    /// <summary>Returns the role to assignable state.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Raise(new RoleReactivated(Id, Name));
    }

    /// <summary>True when this role grants the permission.</summary>
    public bool Grants(string permissionKey) =>
        _permissions.Any(p => string.Equals(p.PermissionKey, permissionKey, StringComparison.Ordinal));

    private void ReplacePermissions(IEnumerable<string> permissionKeys)
    {
        var distinct = (permissionKeys ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var unknown = distinct.Where(k => !PermissionCatalog.IsKnown(k)).ToArray();
        if (unknown.Length > 0)
        {
            throw new DomainException("ROLE-005",
                $"Unknown permission key(s): {string.Join(", ", unknown)}. A privilege must map to a real capability.");
        }

        _permissions.Clear();
        _permissions.AddRange(distinct.Select(k => new RolePermission(k)));
    }

    private static string? Normalize(string? language) =>
        string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant();
}

// ── Domain events (privilege changes are regulated records) ───────────────────

/// <summary>A role was created.</summary>
public sealed record RoleCreated(Guid RoleId, string Name, bool IsSystem) : DomainEvent;

/// <summary>A tenant-defined role was renamed.</summary>
public sealed record RoleRenamed(Guid RoleId, string Name) : DomainEvent;

/// <summary>A role's privileges changed — the grants and revocations, with the operator's reason.</summary>
public sealed record RolePermissionsChanged(
    Guid RoleId,
    string Name,
    IReadOnlyList<string> Granted,
    IReadOnlyList<string> Revoked,
    string Reason) : DomainEvent;

/// <summary>A role was withdrawn from assignment.</summary>
public sealed record RoleDeactivated(Guid RoleId, string Name) : DomainEvent;

/// <summary>A role was returned to assignable state.</summary>
public sealed record RoleReactivated(Guid RoleId, string Name) : DomainEvent;
