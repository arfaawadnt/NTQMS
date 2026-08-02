namespace NT.QAMS.Contracts.IdentityAccess;

/// <summary>
/// TenantIdentifier is the tenant slug; omit it for platform-admin login.
/// MfaCode is required only for accounts with MFA enabled.
/// </summary>
public sealed record LoginRequest(string? TenantIdentifier, string Email, string Password, string? MfaCode);

public sealed record AuthResponse(
    string AccessToken, DateTimeOffset ExpiresAtUtc, string Role, string DisplayName,
    Guid? TenantId, bool MfaRequired, bool MfaEnrollmentRequired = false);

/// <summary>Returned when starting MFA enrollment — show the QR/URI to the user's authenticator app.</summary>
public sealed record MfaEnrollmentResponse(string Secret, string OtpAuthUri);

public sealed record ConfirmMfaRequest(string Code);
/// <summary>
/// Sets or changes the e-signature PIN. The account password is re-verified so a
/// live session alone cannot silently replace a Part 11 signing component.
/// </summary>
public sealed record SetPinRequest(string CurrentPassword, string Pin);

/// <summary>Self-service password change for the signed-in user (identity from the session).</summary>
public sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);
