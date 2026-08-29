using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Domain.Notifications;

namespace NT.QAMS.Application.Notifications;

/// <summary>
/// M-06: routes the two safety-critical incident facts — automatic
/// harm-grade escalation and a signed sentinel-event declaration — to the
/// people who must act on them.
/// <para>
/// Recipient rule (owner decision, 2026-08-30): if the incident has an
/// assignee, that person alone is alerted — they own the response. If it has
/// none yet (escalation fires at report, before triage), the alert goes to the
/// head(s) of the incident's department and to every quality manager, so
/// leadership is reached without waiting for triage.
/// </para>
/// <para>
/// This uses the direct-dispatch path rather than a notification rule because
/// the audience is conditional (assignee vs. leadership) and department-scoped —
/// neither of which the tenant-wide, role-only rule engine can express. Like
/// every dispatch it is idempotent by the source event id, so the escalation and
/// the sentinel declaration each notify exactly once even under at-least-once
/// event redelivery.
/// </para>
/// </summary>
public sealed class IncidentAlertPolicies(IAppDbContext db, NotificationDispatcher dispatcher) :
    INotificationHandler<DomainEventNotification<IncidentEscalated>>,
    INotificationHandler<DomainEventNotification<SentinelDeclared>>
{
    public const string IncidentEscalatedKey = "INC_ESCALATED";
    public const string SentinelDeclaredKey = "INC_SENTINEL";

    public async Task Handle(DomainEventNotification<IncidentEscalated> n, CancellationToken ct)
    {
        var e = n.Event;
        var incident = await LoadAsync(e.IncidentId, ct);
        if (incident is null)
        {
            return;
        }

        var recipients = await ResolveRecipientsAsync(incident, ct);
        var subject = $"Incident escalated: {e.IncidentRef} (harm grade {e.HarmGrade})";
        var body = new StringBuilder()
            .AppendLine($"Incident {e.IncidentRef} has escalated at harm grade {e.HarmGrade}.")
            .AppendLine()
            .AppendLine("It requires immediate review. Open the incident to triage or continue the investigation.")
            .ToString();

        await dispatcher.DispatchToUsersAsync(
            e.EventId, incident.TenantId, IncidentEscalatedKey, recipients, subject, body, ct);
    }

    public async Task Handle(DomainEventNotification<SentinelDeclared> n, CancellationToken ct)
    {
        var e = n.Event;
        var incident = await LoadAsync(e.IncidentId, ct);
        if (incident is null)
        {
            return;
        }

        var recipients = await ResolveRecipientsAsync(incident, ct);
        var subject = $"SENTINEL EVENT declared: {e.IncidentRef}";
        var body = new StringBuilder()
            .AppendLine($"Incident {e.IncidentRef} has been declared a sentinel event.")
            .AppendLine()
            .AppendLine("The executive-notification protocol applies. Open the incident for the full record.")
            .ToString();

        await dispatcher.DispatchToUsersAsync(
            e.EventId, incident.TenantId, SentinelDeclaredKey, recipients, subject, body, ct);
    }

    private Task<IncidentScope?> LoadAsync(Guid incidentId, CancellationToken ct) =>
        db.Incidents.IgnoreQueryFilters()
            .Where(x => x.Id == incidentId)
            .Select(x => new IncidentScope(x.TenantId, x.AssignedTo, x.DepartmentId))
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// The assignee alone when one exists; otherwise the department head(s) of the
    /// incident's department plus every quality manager. A department head with an
    /// empty working scope is unrestricted and so covers every department; a scoped
    /// head is included only when the incident falls in their department.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>> ResolveRecipientsAsync(IncidentScope incident, CancellationToken ct)
    {
        if (incident.AssignedTo is { } assignee)
        {
            return [assignee];
        }

        var recipients = new HashSet<Guid>();

        var qualityManagers = await db.Users
            .Where(u => u.TenantId == incident.TenantId && u.IsActive && u.Role == UserRole.QualityManager)
            .Select(u => u.Id)
            .ToListAsync(ct);
        recipients.UnionWith(qualityManagers);

        var headQuery = db.Users
            .Where(u => u.TenantId == incident.TenantId && u.IsActive && u.Role == UserRole.DepartmentHead);
        if (incident.DepartmentId is { } departmentId)
        {
            headQuery = headQuery.Where(u =>
                u.DepartmentAccess.Count == 0 || u.DepartmentAccess.Any(d => d.DepartmentId == departmentId));
        }

        recipients.UnionWith(await headQuery.Select(u => u.Id).ToListAsync(ct));

        return recipients;
    }

    private sealed record IncidentScope(Guid TenantId, Guid? AssignedTo, Guid? DepartmentId);
}
