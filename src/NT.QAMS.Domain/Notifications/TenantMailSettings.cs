using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Notifications;

/// <summary>
/// Per-tenant mail <em>sender identity</em> and branding for notifications of the
/// Mail type. This holds only what a tenant administrator legitimately owns — the
/// From display name and address, an optional reply-to, whether mail is enabled at
/// all, and the brand accent and footer for the HTML template. It deliberately does
/// <b>not</b> hold SMTP transport credentials: the relay host/user/password stay in
/// secured server configuration, so no reversible secret is ever persisted in the
/// tenant database (a 21 CFR Part 11 / SEC-001 posture decision).
/// </summary>
public sealed class TenantMailSettings : AggregateRoot, ITenantScoped
{
    private TenantMailSettings()
    {
        FromName = null!;
        FromAddress = null!;
    }

    public Guid TenantId { get; set; }

    /// <summary>The display name recipients see as the sender, e.g. "Acme Labs Quality".</summary>
    public string FromName { get; private set; }

    /// <summary>The From address notifications are sent as.</summary>
    public string FromAddress { get; private set; }

    /// <summary>Optional Reply-To address; when null, replies go to the From address.</summary>
    public string? ReplyTo { get; private set; }

    /// <summary>When false, Mail-type notifications are suppressed for the tenant (in-app feed still delivers).</summary>
    public bool Enabled { get; private set; }

    /// <summary>Optional brand accent colour (hex <c>#RRGGBB</c>) for the HTML template header.</summary>
    public string? BrandColor { get; private set; }

    /// <summary>Optional footer line for the HTML template (e.g. a confidentiality notice).</summary>
    public string? FooterNote { get; private set; }

    public static TenantMailSettings Create(
        string fromName, string fromAddress, string? replyTo, bool enabled,
        string? brandColor, string? footerNote)
    {
        var settings = new TenantMailSettings();
        settings.Apply(fromName, fromAddress, replyTo, enabled, brandColor, footerNote);
        settings.Raise(new MailSettingsChanged(settings.Id, settings.FromAddress, settings.Enabled));
        return settings;
    }

    public void Update(
        string fromName, string fromAddress, string? replyTo, bool enabled,
        string? brandColor, string? footerNote)
    {
        Apply(fromName, fromAddress, replyTo, enabled, brandColor, footerNote);
        Raise(new MailSettingsChanged(Id, FromAddress, Enabled));
    }

    private void Apply(
        string fromName, string fromAddress, string? replyTo, bool enabled,
        string? brandColor, string? footerNote)
    {
        if (string.IsNullOrWhiteSpace(fromName))
        {
            throw new DomainException("MAIL-001", "A sender display name is required.");
        }

        if (string.IsNullOrWhiteSpace(fromAddress) || !LooksLikeEmail(fromAddress))
        {
            throw new DomainException("MAIL-002", "A valid sender e-mail address is required.");
        }

        if (!string.IsNullOrWhiteSpace(replyTo) && !LooksLikeEmail(replyTo))
        {
            throw new DomainException("MAIL-003", "The reply-to must be a valid e-mail address.");
        }

        if (!string.IsNullOrWhiteSpace(brandColor) && !LooksLikeHexColor(brandColor))
        {
            throw new DomainException("MAIL-004", "The brand colour must be a hex value like #1E3A5F.");
        }

        FromName = fromName.Trim();
        FromAddress = fromAddress.Trim();
        ReplyTo = string.IsNullOrWhiteSpace(replyTo) ? null : replyTo.Trim();
        Enabled = enabled;
        BrandColor = string.IsNullOrWhiteSpace(brandColor) ? null : brandColor.Trim();
        FooterNote = string.IsNullOrWhiteSpace(footerNote) ? null : footerNote.Trim();
    }

    /// <summary>A deliberately conservative shape check — full RFC validation belongs to the SMTP server, not here.</summary>
    private static bool LooksLikeEmail(string value)
    {
        var at = value.Trim().IndexOf('@');
        return at > 0 && at < value.Trim().Length - 1 && value.Trim().IndexOf('@', at + 1) < 0;
    }

    private static bool LooksLikeHexColor(string value) =>
        value.Length == 7 && value[0] == '#'
        && value[1..].All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
}

/// <summary>Raised when a tenant's mail sender identity is created or changed (into the audit trail).</summary>
public sealed record MailSettingsChanged(Guid SettingsId, string FromAddress, bool Enabled) : DomainEvent;
