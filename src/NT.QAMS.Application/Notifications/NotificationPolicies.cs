using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.Domain.Tenancy;

namespace NT.QAMS.Application.Notifications;

/// <summary>
/// Routes domain events into the notification dispatcher. Producers never call
/// notifications inline — this is the only path, so API-, job- and saga-originated
/// changes all notify identically.
/// Events that don't carry TenantId (doc/NC events raised before that field was
/// needed) are resolved from the source aggregate.
/// </summary>
public sealed class NotificationEventPolicies(IAppDbContext db, NotificationDispatcher dispatcher) :
    INotificationHandler<DomainEventNotification<NcRaised>>,
    INotificationHandler<DomainEventNotification<DocumentPublished>>,
    INotificationHandler<DomainEventNotification<CalibrationDue>>,
    INotificationHandler<DomainEventNotification<EquipmentLockedOut>>,
    INotificationHandler<DomainEventNotification<CompetencyExpired>>,
    INotificationHandler<DomainEventNotification<HighResidualRisk>>,
    INotificationHandler<DomainEventNotification<SupplierSuspended>>,
    INotificationHandler<DomainEventNotification<EscalationTriggered>>,
    INotificationHandler<DomainEventNotification<ReferenceStandardExpired>>,
    INotificationHandler<DomainEventNotification<HighImpartialityRiskDeclared>>,
    INotificationHandler<DomainEventNotification<ManagementReviewScheduled>>
{
    public const string NcRaisedKey = "NC_RAISED";
    public const string DocumentPublishedKey = "DOC_PUBLISHED";
    public const string CalibrationDueKey = "EQUIP_CALIB_DUE";
    public const string EquipmentLockedOutKey = "EQUIP_LOCKED_OUT";
    public const string CompetencyExpiredKey = "COMP_EXPIRED";
    public const string HighResidualRiskKey = "RISK_HIGH_RESIDUAL";
    public const string SupplierSuspendedKey = "SUP_SUSPENDED";
    public const string EscalationKey = "SLA_ESCALATED";
    public const string DocumentReviewDueKey = "DOC_REVIEW_DUE";
    public const string ReferenceStandardExpiredKey = "STD_EXPIRED";
    public const string HighImpartialityRiskKey = "COI_HIGH";
    public const string ReviewScheduledKey = "MRV_SCHEDULED";

    public async Task Handle(DomainEventNotification<NcRaised> n, CancellationToken ct)
    {
        var e = n.Event;
        var tenantId = await db.Nonconformances.IgnoreQueryFilters()
            .Where(x => x.Id == e.NcId).Select(x => x.TenantId).SingleAsync(ct);
        await dispatcher.DispatchAsync(e.EventId, tenantId, NcRaisedKey, new Dictionary<string, string>
        {
            ["ref"] = e.NcRef, ["title"] = e.Title,
            ["severity"] = e.Severity.ToString(), ["rpn"] = e.Rpn.ToString(),
        }, ct);
    }

    public async Task Handle(DomainEventNotification<DocumentPublished> n, CancellationToken ct)
    {
        var e = n.Event;
        var tenantId = await db.Documents.IgnoreQueryFilters()
            .Where(x => x.Id == e.DocumentId).Select(x => x.TenantId).SingleAsync(ct);
        await dispatcher.DispatchAsync(e.EventId, tenantId, DocumentPublishedKey, new Dictionary<string, string>
        {
            ["ref"] = e.Code, ["title"] = e.Title, ["version"] = e.Version,
        }, ct);
    }

    public Task Handle(DomainEventNotification<CalibrationDue> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, CalibrationDueKey,
            new Dictionary<string, string>
            {
                ["ref"] = n.Event.Code, ["title"] = n.Event.Name,
                ["due"] = n.Event.DueDate.ToString("yyyy-MM-dd"),
            }, ct);

    /// <summary>
    /// A scheduled management review is announced to its named participants, not
    /// to a role: the audience is the explicit invitation list on the record, so
    /// this uses the direct-dispatch path rather than a notification rule.
    /// </summary>
    public async Task Handle(DomainEventNotification<ManagementReviewScheduled> n, CancellationToken ct)
    {
        var e = n.Event;
        if (e.ParticipantUserIds.Length == 0)
        {
            return;
        }

        var tenantId = await db.ManagementReviews.IgnoreQueryFilters()
            .Where(x => x.Id == e.ReviewId).Select(x => x.TenantId).SingleAsync(ct);

        var subject = $"Management review scheduled: {e.Title} ({e.ReviewRef}) on {e.ReviewDate:yyyy-MM-dd}";
        var body = new StringBuilder()
            .AppendLine($"You are invited to the management review \"{e.Title}\" ({e.ReviewRef}).")
            .AppendLine()
            .AppendLine($"Date: {e.ReviewDate:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(e.MeetingLink))
        {
            body.AppendLine($"Meeting link: {e.MeetingLink}");
        }

        if (!string.IsNullOrWhiteSpace(e.Agenda))
        {
            body.AppendLine().AppendLine("Agenda:").AppendLine(e.Agenda);
        }

        body.AppendLine().Append("This invitation was sent by NT.QAMS on behalf of the review organiser.");

        await dispatcher.DispatchToUsersAsync(
            e.EventId, tenantId, ReviewScheduledKey, e.ParticipantUserIds, subject, body.ToString(), ct);
    }

    public Task Handle(DomainEventNotification<EquipmentLockedOut> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, EquipmentLockedOutKey,
            new Dictionary<string, string> { ["ref"] = n.Event.Code, ["title"] = n.Event.Name }, ct);

    public Task Handle(DomainEventNotification<HighImpartialityRiskDeclared> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, HighImpartialityRiskKey,
            new Dictionary<string, string>
            {
                ["ref"] = n.Event.ConflictRef, ["title"] = n.Event.RelatedParty,
            }, ct);

    public Task Handle(DomainEventNotification<ReferenceStandardExpired> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, ReferenceStandardExpiredKey,
            new Dictionary<string, string>
            {
                ["ref"] = n.Event.StandardRef, ["title"] = n.Event.Name,
                ["due"] = n.Event.ExpiredOn.ToString("yyyy-MM-dd"),
            }, ct);

    public Task Handle(DomainEventNotification<CompetencyExpired> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, CompetencyExpiredKey,
            new Dictionary<string, string> { ["title"] = n.Event.Subject }, ct);

    public Task Handle(DomainEventNotification<HighResidualRisk> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, HighResidualRiskKey,
            new Dictionary<string, string>
            {
                ["ref"] = n.Event.RiskRef, ["title"] = n.Event.Title,
                ["rpn"] = n.Event.ResidualRpn.ToString(),
            }, ct);

    public Task Handle(DomainEventNotification<SupplierSuspended> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, SupplierSuspendedKey,
            new Dictionary<string, string>
            {
                ["ref"] = n.Event.SupplierRef, ["title"] = n.Event.Name, ["reason"] = n.Event.Reason,
            }, ct);

    public Task Handle(DomainEventNotification<EscalationTriggered> n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.Event.EventId, n.Event.TenantId, EscalationKey,
            new Dictionary<string, string>
            {
                ["ref"] = n.Event.SubjectRef, ["level"] = n.Event.Level.ToString(),
            }, ct);
}

/// <summary>Seeds the default notification rules for a freshly provisioned tenant.</summary>
public sealed class SeedDefaultNotificationRulesPolicy(IAppDbContext db, ICurrentTenantSetter tenantSetter)
    : INotificationHandler<DomainEventNotification<TenantProvisioned>>
{
    public async Task Handle(DomainEventNotification<TenantProvisioned> n, CancellationToken ct)
    {
        var tenantId = n.Event.TenantId;
        tenantSetter.Set(tenantId);

        if (await db.NotificationRules.AnyAsync(r => r.TenantId == tenantId, ct))
        {
            return; // Idempotent.
        }

        (string Key, string Subject, string Body)[] defaults =
        [
            (NotificationEventPolicies.NcRaisedKey,
                "NC raised: {ref} — {title}", "Nonconformance {ref} (severity {severity}, RPN {rpn}) awaits triage."),
            (NotificationEventPolicies.DocumentPublishedKey,
                "Document published: {ref} v{version}", "{title} ({ref}) version {version} is now the controlled copy."),
            (NotificationEventPolicies.CalibrationDueKey,
                "Calibration due: {ref}", "{title} ({ref}) calibration was due {due}."),
            (NotificationEventPolicies.EquipmentLockedOutKey,
                "EQUIPMENT LOCKED OUT: {ref}", "{title} ({ref}) is out of service — calibration grace period exhausted."),
            (NotificationEventPolicies.CompetencyExpiredKey,
                "Competency expired: {title}", "An authorization for {title} expired and requires requalification."),
            (NotificationEventPolicies.HighResidualRiskKey,
                "High residual risk: {ref}", "{title} ({ref}) has residual RPN {rpn} — above the acceptance threshold."),
            (NotificationEventPolicies.SupplierSuspendedKey,
                "Supplier suspended: {title}", "{title} ({ref}) was suspended: {reason}"),
            (NotificationEventPolicies.EscalationKey,
                "Escalation L{level}: {ref}", "{ref} is overdue and has escalated to level {level}."),
        ];

        foreach (var (key, subject, body) in defaults)
        {
            var rule = NotificationRule.Create(key, "QualityManager,TenantAdmin", emailEnabled: true, subject, body);
            rule.TenantId = tenantId;
            db.NotificationRules.Add(rule);
        }

        await db.SaveChangesAsync(ct);
    }
}
