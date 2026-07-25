using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Competency;

namespace NT.QAMS.Application.Competency;

/// <summary>
/// §6.2.6 saga: an authorization is only as good as the competency evidencing
/// it. When that competency lapses (expiry → requalification) the dependent
/// authorizations are suspended; when it is revoked they are revoked with the
/// same reason. Idempotent by construction — Suspend/Revoke are skipped for
/// entries already off the Active path; runs from the outbox in a background
/// scope, so the tenant context is set from the event.
/// </summary>
public sealed partial class CompetencyLapseAuthorizationPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    ILogger<CompetencyLapseAuthorizationPolicy> logger) :
    INotificationHandler<DomainEventNotification<CompetencyExpired>>,
    INotificationHandler<DomainEventNotification<CompetencyRevoked>>
{
    public async Task Handle(DomainEventNotification<CompetencyExpired> notification, CancellationToken ct)
    {
        var e = notification.Event;
        tenantSetter.Set(e.TenantId);

        var dependents = await db.TestAuthorizations
            .Where(a => a.CompetencyRecordId == e.CompetencyId
                        && a.Status == TestAuthorizationStatus.Active)
            .ToListAsync(ct);
        dependents.ForEach(a => a.SuspendIfActive(
            $"Competency '{e.Subject}' expired — requalification required."));
        await db.SaveChangesAsync(ct);

        if (dependents.Count > 0)
        {
            LogSuspended(logger, dependents.Count, e.Subject);
        }
    }

    public async Task Handle(DomainEventNotification<CompetencyRevoked> notification, CancellationToken ct)
    {
        var e = notification.Event;
        tenantSetter.Set(e.TenantId);

        var dependents = await db.TestAuthorizations
            .Where(a => a.CompetencyRecordId == e.CompetencyId
                        && (a.Status == TestAuthorizationStatus.Active
                            || a.Status == TestAuthorizationStatus.Suspended))
            .ToListAsync(ct);
        foreach (var authorization in dependents)
        {
            authorization.Revoke(e.RevokedBy, $"Competency '{e.Subject}' revoked: {e.Reason}");
        }

        await db.SaveChangesAsync(ct);

        if (dependents.Count > 0)
        {
            LogRevoked(logger, dependents.Count, e.Subject);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Suspended {Count} authorization(s) after competency '{Subject}' expired")]
    private static partial void LogSuspended(ILogger logger, int count, string subject);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked {Count} authorization(s) after competency '{Subject}' was revoked")]
    private static partial void LogRevoked(ILogger logger, int count, string subject);
}
