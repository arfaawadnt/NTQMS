using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.Notifications;

/// <summary>
/// One outbound e-mail: the recipient, subject and (plain-text) body, plus the
/// per-tenant sender identity and branding that the adapter uses to build the From
/// header and the HTML template. Transport credentials are NOT here — they stay in
/// server configuration; this carries only what a tenant administrator owns.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string BodyText,
    string? FromName,
    string? FromAddress,
    string? ReplyTo,
    string TenantName,
    string? BrandColor,
    string? FooterNote);

/// <summary>Email delivery port. Infrastructure supplies SMTP (or a logging no-op when unconfigured).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
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

        // Per-tenant sender identity: mail may be disabled (in-app only), and when
        // enabled it drives the From header and HTML branding. Absent settings mean
        // "enabled, server-default sender" — unchanged behaviour for tenants that
        // have not configured mail.
        var mail = await ResolveMailAsync(tenantId, ct);
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
                    subject, body, rule.EmailEnabled && mail.Enabled));
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

        await SendQueuedAsync(pending, mail, eventKey, ct);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The tenant's resolved mail identity for this dispatch (server default when unset).</summary>
    private sealed record MailIdentity(
        bool Enabled, string TenantName, string? FromName, string? FromAddress, string? ReplyTo,
        string? BrandColor, string? FooterNote);

    private async Task<MailIdentity> ResolveMailAsync(Guid tenantId, CancellationToken ct)
    {
        var tenantName = (await db.Tenants.FindAsync([tenantId], ct))?.Name ?? "NT.QAMS";
        var m = await db.MailSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return m is null
            ? new MailIdentity(true, tenantName, null, null, null, null, null)
            : new MailIdentity(m.Enabled, tenantName, m.FromName, m.FromAddress, m.ReplyTo, m.BrandColor, m.FooterNote);
    }

    private async Task SendQueuedAsync(
        IEnumerable<NotificationDispatch> pending, MailIdentity mail, string eventKey, CancellationToken ct)
    {
        foreach (var dispatch in pending.Where(d => d.EmailStatus == DispatchStatus.Queued))
        {
            try
            {
                await emailSender.SendAsync(new EmailMessage(
                    dispatch.RecipientEmail!, dispatch.Subject, dispatch.Body,
                    mail.FromName, mail.FromAddress, mail.ReplyTo,
                    mail.TenantName, mail.BrandColor, mail.FooterNote), ct);
                dispatch.MarkEmailSent(clock.UtcNow);
            }
            catch (Exception ex)
            {
                dispatch.MarkEmailFailed(ex.Message);
                LogEmailFailed(logger, ex, dispatch.RecipientEmail!, eventKey);
            }
        }
    }

    /// <summary>
    /// Dispatches to a named set of users rather than to rule-matched roles —
    /// for facts with an explicit audience, like a meeting invitation to its
    /// participants. Goes through the same feed rows, idempotency guard and
    /// best-effort email as the rule path, so explicit-audience messages appear
    /// in the in-app feed and the delivery monitor like every other dispatch.
    /// </summary>
    public async Task DispatchToUsersAsync(
        Guid sourceEventId, Guid tenantId, string eventKey,
        IReadOnlyCollection<Guid> recipientUserIds, string subject, string body, CancellationToken ct)
    {
        tenantSetter.Set(tenantId);

        if (await db.NotificationDispatches.AnyAsync(d => d.SourceEventId == sourceEventId, ct))
        {
            return; // Already dispatched — redelivery is a no-op.
        }

        if (recipientUserIds.Count == 0)
        {
            return;
        }

        // Tenant-filtered explicitly: UserAccount is optionally tenant-scoped, so
        // the global filter does not apply. Inactive users are skipped — an
        // invitation to a disabled mailbox is a delivery failure waiting to happen.
        var recipients = await db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && recipientUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);

        var mail = await ResolveMailAsync(tenantId, ct);
        var pending = recipients
            .Select(r => NotificationDispatch.Create(
                sourceEventId, eventKey, r.Id, r.Email, subject, body, emailRequested: mail.Enabled))
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var dispatch in pending)
        {
            db.NotificationDispatches.Add(dispatch);
        }

        // Feed rows first, then best-effort email — same guarantee as the rule path.
        await db.SaveChangesAsync(ct);

        await SendQueuedAsync(pending, mail, eventKey, ct);

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
