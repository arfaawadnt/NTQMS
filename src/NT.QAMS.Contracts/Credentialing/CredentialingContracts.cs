namespace NT.QAMS.Contracts.Credentialing;

// ── Practitioner + write requests ─────────────────────────────────────────────

public sealed record RegisterPractitionerRequest(string FullName, string Specialty);

public sealed record AddLicenceRequest(string Type, string Identifier, string Issuer, DateOnly ExpiresOn);

public sealed record VerifyLicenceRequest(string Source);

public sealed record RequestPrivilegeRequest(string Name);

public sealed record GrantPrivilegeRequest(DateOnly? GrantedUntil);

public sealed record DenyPrivilegeRequest(string Reason);

public sealed record CredentialRequest(DateOnly AppointedUntil);

public sealed record SuspendPractitionerRequest(string Reason);

// ── Read models ───────────────────────────────────────────────────────────────

public sealed record PractitionerListItemDto(
    Guid Id, string PractitionerRef, string FullName, string Specialty, string Status,
    DateOnly? AppointedUntil, int GrantedPrivileges, int VerifiedLicences);

public sealed record LicenceDto(
    Guid Id, string Type, string Identifier, string Issuer, DateOnly ExpiresOn, bool Expired,
    string VerificationStatus, Guid? VerifiedBy, string? VerificationSource, DateTimeOffset? VerifiedAtUtc);

public sealed record PrivilegeDto(Guid Id, string Name, string Status, DateOnly? GrantedUntil, string? DenialReason);

public sealed record PractitionerDetailDto(
    Guid Id, string PractitionerRef, string FullName, string Specialty, string Status,
    DateOnly? AppointedUntil, string? SuspensionReason,
    IReadOnlyList<LicenceDto> Licences, IReadOnlyList<PrivilegeDto> Privileges);

// ── Licence-expiry register (tiered) ──────────────────────────────────────────

public sealed record ExpiringCredentialDto(
    Guid PractitionerId, string PractitionerRef, string FullName, Guid LicenceId, string Type,
    string Identifier, DateOnly ExpiresOn, int DaysToExpiry, string Tier);

// ── Point-of-care privilege verification ──────────────────────────────────────

public sealed record PrivilegeCheckResultDto(
    Guid PractitionerId, string PractitionerRef, string FullName, string PrivilegeName,
    bool Holds, string PractitionerStatus, string? Detail);
