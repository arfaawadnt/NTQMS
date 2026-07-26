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
public sealed record SetPinRequest(string Pin);
