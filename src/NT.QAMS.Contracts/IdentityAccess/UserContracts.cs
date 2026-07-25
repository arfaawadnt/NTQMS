namespace NT.QAMS.Contracts.IdentityAccess;

/// <summary>A tenant user as shown in the administration screens.</summary>
public sealed record UserDto(
    Guid Id, string Email, string DisplayName, string Role, bool IsActive, bool MfaEnabled);

public sealed record RegisterUserRequest(
    string Email, string DisplayName, string Role, string InitialPassword);

public sealed record ChangeUserRoleRequest(string Role);

public sealed record ResetUserPasswordRequest(string NewPassword);

/// <summary>Lightweight directory entry for user pickers (no email, no security fields).</summary>
public sealed record UserDirectoryEntryDto(Guid Id, string DisplayName, string Role);
