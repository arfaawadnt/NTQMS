namespace NT.QAMS.Contracts.IdentityAccess;

/// <summary>A tenant user as shown in the administration screens.</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    bool MfaEnabled,
    Guid? RoleId,
    string? RoleName,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> DepartmentIds,
    string? PreferredLanguage,
    /// <summary>Whether an e-signature PIN is on file — the fact only, never the value.</summary>
    bool PinConfigured = false);

/// <summary>
/// <paramref name="InitialPin"/> is optional: when supplied the account starts
/// with an admin-issued signing PIN (ledgered as PIN_ADMIN_SET); the user can
/// rotate it from their account menu.
/// </summary>
public sealed record RegisterUserRequest(
    string Email, string DisplayName, string Role, string InitialPassword, Guid? RoleId = null,
    string? InitialPin = null);

public sealed record ChangeUserRoleRequest(string Role);

public sealed record ResetUserPasswordRequest(string NewPassword);

/// <summary>Admin-issued e-signature PIN for a user (set or reset).</summary>
public sealed record SetUserPinRequest(string Pin);

/// <summary>Lightweight directory entry for user pickers (no email, no security fields).</summary>
public sealed record UserDirectoryEntryDto(Guid Id, string DisplayName, string Role);

/// <summary>Self-service password rotation (usable while the password is expired).</summary>
public sealed record ChangePasswordRequest(
    string? TenantIdentifier, string Email, string CurrentPassword, string NewPassword);
