using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Notifications;

namespace NT.QAMS.Infrastructure.Email;

/// <summary>
/// SMTP adapter. Configured entirely by environment (Smtp__Host etc.); when no
/// host is configured, DI registers <see cref="LoggingEmailSender"/> instead —
/// the in-app feed still works, email quietly logs. No credentials in code.
/// </summary>
public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"]!;
        var port = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
        var from = configuration["Smtp:From"] ?? configuration["Smtp:User"] ?? "ntqams@localhost";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = !string.Equals(configuration["Smtp:Ssl"], "false", StringComparison.OrdinalIgnoreCase),
        };

        var user = configuration["Smtp:User"];
        if (!string.IsNullOrWhiteSpace(user))
        {
            client.Credentials = new NetworkCredential(user, configuration["Smtp:Password"]);
        }

        using var message = new MailMessage(from, to, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}

public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        LogSkipped(logger, to, subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SMTP not configured — email to {To} skipped: {Subject}")]
    private static partial void LogSkipped(ILogger logger, string to, string subject);
}
