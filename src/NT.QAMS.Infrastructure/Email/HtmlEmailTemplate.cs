using System.Net;
using System.Text;
using NT.QAMS.Application.Notifications;

namespace NT.QAMS.Infrastructure.Email;

/// <summary>
/// Renders a notification as a branded, responsive HTML e-mail. Uses table layout
/// and fully inlined styles — the only combination mail clients (Outlook included)
/// render reliably; there is no external CSS or web font. The brand accent comes
/// from the tenant's mail settings (falling back to the NT brand navy), and every
/// piece of caller-supplied text is HTML-escaped, so a template value can never
/// inject markup into the message.
/// </summary>
internal static class HtmlEmailTemplate
{
    private const string DefaultAccent = "#1E3A5F"; // --nt-navy

    public static string Render(EmailMessage message)
    {
        var accent = IsHexColor(message.BrandColor) ? message.BrandColor! : DefaultAccent;
        var tenant = WebUtility.HtmlEncode(message.TenantName);
        var subject = WebUtility.HtmlEncode(message.Subject);

        var body = new StringBuilder();
        foreach (var line in message.BodyText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) { continue; }
            body.Append("<p style=\"margin:0 0 12px;font-size:14px;line-height:1.55;color:#3B4658;\">")
                .Append(WebUtility.HtmlEncode(trimmed))
                .Append("</p>");
        }

        var footer = string.IsNullOrWhiteSpace(message.FooterNote)
            ? "This is an automated message from your NT.QAMS quality management system."
            : WebUtility.HtmlEncode(message.FooterNote!);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#EEF1F5;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#EEF1F5;padding:24px 0;">
                <tr><td align="center">
                  <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:600px;max-width:92%;background:#FFFFFF;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(30,58,95,.12);">
                    <tr><td style="background:{{accent}};padding:20px 28px;">
                      <div style="font-size:18px;font-weight:700;color:#FFFFFF;">{{tenant}}</div>
                      <div style="font-size:12px;color:rgba(255,255,255,.82);margin-top:2px;">Quality Management System</div>
                    </td></tr>
                    <tr><td style="padding:26px 28px 8px;">
                      <div style="font-size:16px;font-weight:700;color:{{accent}};margin-bottom:14px;">{{subject}}</div>
                      {{body}}
                    </td></tr>
                    <tr><td style="padding:8px 28px 26px;">
                      <div style="height:1px;background:#E1E5EC;margin:8px 0 14px;"></div>
                      <div style="font-size:11.5px;line-height:1.5;color:#8A93A2;">{{footer}}</div>
                    </td></tr>
                  </table>
                  <div style="font-size:10.5px;color:#A6ADB8;margin-top:14px;">NT.QAMS · 21 CFR Part 11 compliant quality management</div>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#'
        && value[1..].All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
}
