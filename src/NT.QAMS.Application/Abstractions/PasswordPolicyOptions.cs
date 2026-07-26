namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// Tenant-wide password policy (21 CFR Part 11 §11.300): maximum password age
/// before rotation is forced at login, and how many retired hashes the reuse
/// ban checks. MaxAgeDays = 0 disables aging.
/// </summary>
public sealed record PasswordPolicyOptions(int MaxAgeDays = 90, int HistoryDepth = 5);

/// <summary>
/// Security-enforcement options (21 CFR Part 11 §11.10(d) / Annex 11 §12).
/// When <see cref="RequireMfaForPrivilegedRoles"/> is on, a privileged user
/// (PlatformAdmin / TenantAdmin) who has not enrolled MFA is issued only an
/// enrollment-scoped session until they set it up. Defaults off so it is an
/// explicit, per-environment decision (on in production).
/// </summary>
public sealed record SecurityOptions(bool RequireMfaForPrivilegedRoles = false);
