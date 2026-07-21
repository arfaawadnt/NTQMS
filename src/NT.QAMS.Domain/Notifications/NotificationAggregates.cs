using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Notifications;

public enum DispatchStatus { Queued, Sent, Failed }

/// <summary>
/// Notification rule: when {EventKey} happens, tell users holding
/// {RecipientRoles} via in-app (always) and email (if enabled). Templates use
/// {placeholder} tokens filled from the event context.
/// </summary>
public sealed class NotificationRule : AggregateRoot, ITenantScoped
{
    private NotificationRule()
    {
        EventKey = null!;
        RecipientRoles = null!;
        SubjectTemplate = null!;
        BodyTemplate = null!;
    }

    public Guid TenantId { get; set; }
    public string EventKey { get; private set; }
    /// <summary>Comma-separated role names (canonical set), e.g. "QualityManager,TenantAdmin".</summary>
    public string RecipientRoles { get; private set; }
    public bool EmailEnabled { get; private set; }
    public string SubjectTemplate { get; private set; }
    public string BodyTemplate { get; private set; }
    public bool IsActive { get; private set; }

    public static NotificationRule Create(
        string eventKey, string recipientRoles, bool emailEnabled,
        string subjectTemplate, string bodyTemplate)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            throw new DomainException("NTF-001", "An event key is required (e.g. NC_RAISED).");
        }

        if (string.IsNullOrWhiteSpace(recipientRoles))
        {
            throw new DomainException("NTF-002", "At least one recipient role is required.");
        }

        if (string.IsNullOrWhiteSpace(subjectTemplate))
        {
            throw new DomainException("NTF-003", "A subject template is required.");
        }

        return new NotificationRule
        {
            EventKey = eventKey.Trim().ToUpperInvariant(),
            RecipientRoles = recipientRoles.Trim(),
            EmailEnabled = emailEnabled,
            SubjectTemplate = subjectTemplate.Trim(),
            BodyTemplate = bodyTemplate?.Trim() ?? string.Empty,
            IsActive = true,
        };
    }

    public void Update(string recipientRoles, bool emailEnabled, string subjectTemplate, string bodyTemplate)
    {
        if (string.IsNullOrWhiteSpace(recipientRoles) || string.IsNullOrWhiteSpace(subjectTemplate))
        {
            throw new DomainException("NTF-004", "Recipient roles and subject template are required.");
        }

        RecipientRoles = recipientRoles.Trim();
        EmailEnabled = emailEnabled;
        SubjectTemplate = subjectTemplate.Trim();
        BodyTemplate = bodyTemplate?.Trim() ?? string.Empty;
    }

    public void Deactivate() => IsActive = false;
}

/// <summary>
/// One delivered (or attempted) notification to one recipient. The in-app feed
/// IS this table; email delivery status is recorded per attempt. SourceEventId
/// is the idempotency key against at-least-once event delivery.
/// </summary>
public sealed class NotificationDispatch : AggregateRoot, ITenantScoped
{
    private NotificationDispatch()
    {
        EventKey = null!;
        Subject = null!;
        Body = null!;
    }

    public Guid TenantId { get; set; }
    public Guid SourceEventId { get; private set; }
    public string EventKey { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string? RecipientEmail { get; private set; }
    public string Subject { get; private set; }
    public string Body { get; private set; }
    public DispatchStatus EmailStatus { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public bool ReadByRecipient { get; private set; }

    public static NotificationDispatch Create(
        Guid sourceEventId, string eventKey, Guid recipientUserId, string? recipientEmail,
        string subject, string body, bool emailRequested)
    {
        return new NotificationDispatch
        {
            SourceEventId = sourceEventId,
            EventKey = eventKey,
            RecipientUserId = recipientUserId,
            RecipientEmail = recipientEmail,
            Subject = subject,
            Body = body,
            EmailStatus = emailRequested && recipientEmail is not null
                ? DispatchStatus.Queued
                : DispatchStatus.Sent, // in-app only: nothing further to deliver
        };
    }

    public void MarkEmailSent(DateTimeOffset at)
    {
        EmailStatus = DispatchStatus.Sent;
        SentAtUtc = at;
        Error = null;
    }

    public void MarkEmailFailed(string error)
    {
        EmailStatus = DispatchStatus.Failed;
        Error = error.Length > 1500 ? error[..1500] : error;
    }

    public void MarkRead() => ReadByRecipient = true;
}
