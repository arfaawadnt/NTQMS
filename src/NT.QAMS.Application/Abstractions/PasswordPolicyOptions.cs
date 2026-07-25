namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// Tenant-wide password policy (21 CFR Part 11 §11.300): maximum password age
/// before rotation is forced at login, and how many retired hashes the reuse
/// ban checks. MaxAgeDays = 0 disables aging.
/// </summary>
public sealed record PasswordPolicyOptions(int MaxAgeDays = 90, int HistoryDepth = 5);
