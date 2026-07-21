namespace NT.QAMS.Domain.Tenancy;

/// <summary>
/// Per-tenant configuration (owned value object, 1:1 with the tenant).
/// Defaults per the SRS: password expiry 90 days, calibration reminder 30 days,
/// SOP expiry reminder 3 months. Each consuming context reads its own slice.
/// </summary>
public sealed record TenantSettings
{
    public int PasswordExpiryDays { get; init; } = 90;
    public int CalibrationReminderDays { get; init; } = 30;
    public int SopExpiryReminderMonths { get; init; } = 3;
    public string DefaultLanguage { get; init; } = "en";
    public string TimeZone { get; init; } = "UTC";

    public static TenantSettings Default => new();
}
