using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.IdentityAccess;

/// <summary>
/// Canonical role set (seed defaults; full editable role/privilege matrix is a
/// later deliverable per the architecture).
/// </summary>
public enum UserRole
{
    PlatformAdmin = 0,
    TenantAdmin = 1,
    QualityManager = 2,
    DepartmentHead = 3,
    Analyst = 4,
    ExternalAuditor = 5,
}

/// <summary>
/// A user account. Deliberately NOT ITenantScoped: platform administrators have
/// no tenant, and authentication itself is the access control for this table —
/// handlers always filter by (TenantId, Email) explicitly.
/// Carries the authentication-hardening state: lockout (FR-AUTH-02), TOTP MFA
/// (FR-AUTH-01), and the salted e-signature PIN hash (21 CFR Part 11).
/// </summary>
public sealed class UserAccount : AggregateRoot
{
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 30;

    private readonly List<UserBranchAccess> _branchAccess = [];
    private readonly List<UserDepartmentAccess> _departmentAccess = [];

    private UserAccount()
    {
        Email = null!;
        DisplayName = null!;
        PasswordHash = null!;
    }

    public Guid? TenantId { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }

    /// <summary>When the password was last set; null forces no aging until first rotation is stamped.</summary>
    public DateTimeOffset? PasswordChangedAtUtc { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    // Lockout state.
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    // MFA (TOTP). Secret is set at enrollment; MfaEnabled flips on confirmation.
    public string? MfaSecret { get; private set; }
    public bool MfaEnabled { get; private set; }

    // E-signature PIN (salted hash; never stored or compared in plaintext).
    public string? PinHash { get; private set; }

    /// <summary>
    /// The tenant-defined role this user holds, once the tenant has moved to
    /// configurable privileges. Null while the account still relies on the
    /// built-in <see cref="Role"/> tier alone (seeded accounts before migration).
    /// </summary>
    public Guid? RoleId { get; private set; }

    /// <summary>
    /// Interface language for this user. Null inherits the role default, then the
    /// tenant default — a user's own choice always wins over both.
    /// </summary>
    public string? PreferredLanguage { get; private set; }

    /// <summary>
    /// Branches this user may work in. <b>Empty means unrestricted</b> (the whole
    /// tenant), which is how existing accounts keep working after the upgrade; any
    /// entry turns it into a closed list enforced in the data layer.
    /// </summary>
    public IReadOnlyList<UserBranchAccess> BranchAccess => _branchAccess;

    /// <summary>Departments this user may work in. Empty means unrestricted.</summary>
    public IReadOnlyList<UserDepartmentAccess> DepartmentAccess => _departmentAccess;

    public static UserAccount Create(
        Guid? tenantId, string email, string displayName, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainException("USER-001", "A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("USER-002", "Display name is required.");
        }

        if (role == UserRole.PlatformAdmin && tenantId is not null)
        {
            throw new DomainException("USER-003", "Platform administrators cannot belong to a tenant.");
        }

        if (role != UserRole.PlatformAdmin && tenantId is null)
        {
            throw new DomainException("USER-004", "Tenant users must belong to a tenant.");
        }

        return new UserAccount
        {
            TenantId = tenantId,
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Changes the user's role. Platform-admin is not assignable to a tenant user.</summary>
    public void ChangeRole(UserRole role)
    {
        if (role == UserRole.PlatformAdmin && TenantId is not null)
        {
            throw new DomainException("USER-005", "A tenant user cannot be made a platform administrator.");
        }

        Role = role;
    }

    /// <summary>Replaces the password hash (administrative reset) and clears any lockout.</summary>
    public void ResetPassword(string passwordHash, DateTimeOffset? at = null)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("USER-006", "A password hash is required.");
        }

        PasswordHash = passwordHash;
        PasswordChangedAtUtc = at;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }

    /// <summary>Self-service password change (Part 11 §11.300(b) aging compliance) — stamps the rotation instant.</summary>
    public void ChangePassword(string newPasswordHash, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException("USER-006", "A password hash is required.");
        }

        PasswordHash = newPasswordHash;
        PasswordChangedAtUtc = at;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntilUtc is { } until && until > now;

    /// <summary>Assigns the tenant-defined role that carries this user's privileges.</summary>
    public void AssignRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainException("USER-010", "A role is required.");
        }

        RoleId = roleId;
        Raise(new UserRoleAssigned(Id, roleId));
    }

    /// <summary>Sets the user's interface language; null falls back to role, then tenant.</summary>
    public void SetPreferredLanguage(string? language) =>
        PreferredLanguage = string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant();

    /// <summary>
    /// Replaces the user's working scope. An empty list means unrestricted; that is
    /// a deliberate widening of access, so it is raised as its own auditable fact
    /// rather than looking like a routine edit.
    /// </summary>
    public void SetScope(IEnumerable<Guid> branchIds, IEnumerable<Guid> departmentIds)
    {
        var branches = Distinct(branchIds);
        var departments = Distinct(departmentIds);

        _branchAccess.Clear();
        _branchAccess.AddRange(branches.Select(id => new UserBranchAccess(id)));
        _departmentAccess.Clear();
        _departmentAccess.AddRange(departments.Select(id => new UserDepartmentAccess(id)));

        Raise(new UserScopeChanged(Id, branches, departments, IsUnrestricted: branches.Count == 0 && departments.Count == 0));
    }

    /// <summary>True when no branch restriction applies — the user sees the whole tenant.</summary>
    public bool HasUnrestrictedBranchAccess => _branchAccess.Count == 0;

    /// <summary>True when no department restriction applies.</summary>
    public bool HasUnrestrictedDepartmentAccess => _departmentAccess.Count == 0;

    private static List<Guid> Distinct(IEnumerable<Guid> ids) =>
        (ids ?? []).Where(id => id != Guid.Empty).Distinct().ToList();

    /// <summary>Records a failed authentication factor; locks the account at the threshold.</summary>
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedAttempts)
        {
            LockedUntilUtc = now.AddMinutes(LockoutMinutes);
            FailedLoginAttempts = 0;
            Raise(new UserLockedOut(Id, Email, LockedUntilUtc.Value));
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }

    /// <summary>Stores the TOTP secret; MFA is not active until confirmed with a valid code.</summary>
    public void EnrollMfa(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new DomainException("MFA-001", "A TOTP secret is required to enroll.");
        }

        MfaSecret = secret;
        MfaEnabled = false;
    }

    public void ConfirmMfa()
    {
        if (string.IsNullOrWhiteSpace(MfaSecret))
        {
            throw new DomainException("MFA-002", "MFA has not been enrolled.");
        }

        MfaEnabled = true;
    }

    public void SetPin(string pinHash)
    {
        if (string.IsNullOrWhiteSpace(pinHash))
        {
            throw new DomainException("PIN-001", "A PIN hash is required.");
        }

        PinHash = pinHash;
    }
}

public sealed record UserLockedOut(Guid UserId, string Email, DateTimeOffset LockedUntilUtc) : DomainEvent;

/// <summary>A branch the user is permitted to work in. Owned by <see cref="UserAccount"/>.</summary>
public sealed class UserBranchAccess
{
    private UserBranchAccess() { }

    internal UserBranchAccess(Guid branchId) => BranchId = branchId;

    public Guid BranchId { get; private set; }
}

/// <summary>A department the user is permitted to work in. Owned by <see cref="UserAccount"/>.</summary>
public sealed class UserDepartmentAccess
{
    private UserDepartmentAccess() { }

    internal UserDepartmentAccess(Guid departmentId) => DepartmentId = departmentId;

    public Guid DepartmentId { get; private set; }
}

/// <summary>The user was assigned a tenant-defined role (an access-control change).</summary>
public sealed record UserRoleAssigned(Guid UserId, Guid RoleId) : DomainEvent;

/// <summary>
/// The user's branch/department working scope changed. <paramref name="IsUnrestricted"/>
/// records the widest case explicitly, because "no restriction" is the state an
/// auditor most needs to see stated rather than inferred from an empty list.
/// </summary>
public sealed record UserScopeChanged(
    Guid UserId,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> DepartmentIds,
    bool IsUnrestricted) : DomainEvent;
