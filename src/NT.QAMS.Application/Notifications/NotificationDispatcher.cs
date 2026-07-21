using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.Notifications;

/// <summary>Email delivery port. Infrastructure supplies SMTP (or a logging no-op when unconfigured).</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>
/// The rule-driven dispatch engine. Given a domain event (key + context), it
/// matches active rules, resolves recipients by role, renders {placeholder}
/// templates, writes one dispatch row per recipient (the in-app feed), and
/// attempts email where enabled. Idempotent by SourceEventId — at-least-once
/// event delivery never duplicates notifications.
/// </summary>
public sealed partial class NotificationDispatcher(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IEmailSender emailSender,
    IClock clock,
    ILogger<NotificationDispatcher> logger)
{
    public async Task DispatchAsync(
        Guid sourceEventId, Guid tenantId, string eventKey,
        IReadOnlyDictionary<string, string> context, CancellationToken ct)
    {
        tenantSetter.Set(tenantId);

        if (await db.NotificationDispatches.AnyAsync(d => d.SourceEventId == sourceEventId, ct))
        {
            return; // Already dispatched — redelivery is a no-op.
        }

        var rules = await db.NotificationRules
            .Where(r => r.IsActive && r.EventKey == eventKey)
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            return;
        }

        var pending = new List<NotificationDispatch>();

        foreach (var rule in rules)
        {
            var roles = rule.RecipientRoles
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();

            var recipients = await db.Users
                .Where(u => u.TenantId == tenantId && u.IsActive && roles.Contains(u.Role.ToString()))
                .Select(u => new { u.Id, u.Email })
                .ToListAsync(ct);

            var subject = Render(rule.SubjectTemplate, context);
            var body = Render(rule.BodyTemplate, context);

            foreach (var recipient in recipients)
            {
                pending.Add(NotificationDispatch.Create(
                    sourceEventId, eventKey, recipient.Id, recipient.Email,
                    subject, body, rule.EmailEnabled));
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var dispatch in pending)
        {
            db.NotificationDispatches.Add(dispatch);
        }

        // Persist the feed rows FIRST (in-app delivery is the guarantee), then
        // attempt email best-effort — a dead SMTP server never loses the record.
        await db.SaveChangesAsync(ct);

        foreach (var dispatch in pending.Where(d => d.EmailStatus == DispatchStatus.Queued))
        {
            try
            {
                await emailSender.SendAsync(dispatch.RecipientEmail!, dispatch.Subject, dispatch.Body, ct);
                dispatch.MarkEmailSent(clock.UtcNow);
            }
            catch (Exception ex)
            {
                dispatch.MarkEmailFailed(ex.Message);
                LogEmailFailed(logger, ex, dispatch.RecipientEmail!, eventKey);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> context)
    {
        var result = template;
        foreach (var (key, value) in context)
        {
            result = result.Replace("{" + key + "}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email to {Recipient} for {EventKey} failed")]
    private static partial void LogEmailFailed(ILogger logger, Exception ex, string recipient, string eventKey);
}
