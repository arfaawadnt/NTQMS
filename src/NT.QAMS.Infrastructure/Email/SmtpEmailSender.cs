using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Notifications;

namespace NT.QAMS.Infrastructure.Email;

/// <summary>
/// SMTP adapter. The transport (host/port/ssl/user/password) is configured entirely
/// by environment (Smtp__Host etc.) — no credentials in code. The <b>sender identity</b>
/// (From name/address, reply-to) and branding come per-tenant on the message; when a
/// tenant has not set one, the server default From applies. Bodies are sent as a
/// branded HTML view with a plain-text alternate. When no host is configured, DI
/// registers <see cref="LoggingEmailSender"/> instead.
/// </summary>
public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"]!;
        var port = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
        var defaultFrom = configuration["Smtp:From"] ?? configuration["Smtp:User"] ?? "ntqams@localhost";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = !string.Equals(configuration["Smtp:Ssl"], "false", StringComparison.OrdinalIgnoreCase),
        };

        var user = configuration["Smtp:User"];
        if (!string.IsNullOrWhiteSpace(user))
        {
            client.Credentials = new NetworkCredential(user, configuration["Smtp:Password"]);
        }

        var fromAddress = string.IsNullOrWhiteSpace(message.FromAddress) ? defaultFrom : message.FromAddress;
        var from = string.IsNullOrWhiteSpace(message.FromName)
            ? new MailAddress(fromAddress)
            : new MailAddress(fromAddress, message.FromName);

        using var mail = new MailMessage { From = from, Subject = message.Subject };
        mail.To.Add(message.To);
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            mail.ReplyToList.Add(new MailAddress(message.ReplyTo));
        }

        // Plain-text alternate first, HTML view second (clients prefer the last view they can render).
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.BodyText, null, MediaTypeNames.Text.Plain));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            HtmlEmailTemplate.Render(message), null, MediaTypeNames.Text.Html));

        await client.SendMailAsync(mail, cancellationToken);
    }
}

public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogSkipped(logger, message.To, message.Subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SMTP not configured — email to {To} skipped: {Subject}")]
    private static partial void LogSkipped(ILogger logger, string to, string subject);
}
