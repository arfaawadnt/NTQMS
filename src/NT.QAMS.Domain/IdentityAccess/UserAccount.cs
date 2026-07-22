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
    public void ResetPassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("USER-006", "A password hash is required.");
        }

        PasswordHash = passwordHash;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntilUtc is { } until && until > now;

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
