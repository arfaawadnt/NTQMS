using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.Application.AuditManagement.Policies;

/// <summary>
/// The cross-module guarantee (customer–supplier contract from the context map):
/// every NC-graded audit finding gets a Nonconformance, and the finding is
/// acknowledged back so the audit's sign-off gate can open.
///
/// Runs from the outbox (at-least-once), so it is idempotent end-to-end:
/// the NC is keyed by SourceRef "auditRef#findingId", and acknowledgment is a
/// no-op when already set. It runs in a background scope with no ambient tenant —
/// the event carries tenant + actor, and this handler sets the tenant context
/// explicitly before touching tenant-scoped data.
/// </summary>
public sealed partial class FindingToNcPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IReferenceNumberGenerator refs,
    ILogger<FindingToNcPolicy> logger)
    : INotificationHandler<DomainEventNotification<FindingRaised>>
{
    public async Task Handle(DomainEventNotification<FindingRaised> notification, CancellationToken ct)
    {
        var evt = notification.Event;

        if (evt.Grade == FindingGrade.Ofi)
        {
            return; // Opportunities for improvement do not demand an NC.
        }

        tenantSetter.Set(evt.TenantId);

        var sourceRef = $"{evt.AuditRef}#{evt.FindingId:N}";

        // Idempotency: at-least-once delivery must not raise duplicate NCs.
        var nc = await db.Nonconformances
            .SingleOrDefaultAsync(n => n.SourceRef == sourceRef, ct);

        if (nc is null)
        {
            var ncRef = await refs.NextAsync(evt.TenantId, "NC", ct);
            var severity = evt.Grade == FindingGrade.MajorNc ? 4 : 2;

            nc = Nonconformance.Raise(
                ncRef,
                $"Audit finding {evt.AuditRef}: {Truncate(evt.Description, 250)}",
                evt.Description,
                severity,
                likelihood: 3,
                NcSourceType.Audit,
                evt.RaisedBy,
                sourceRef);
            nc.TenantId = evt.TenantId;
            nc.Submit(); // Source-driven NCs enter the register as Raised, ready for triage.

            db.Nonconformances.Add(nc);
            await db.SaveChangesAsync(ct);
            LogNcRaised(logger, nc.NcRef, evt.AuditRef, evt.FindingId);
        }

        var audit = await db.Audits
            .Include(a => a.Findings)
            .SingleOrDefaultAsync(a => a.Id == evt.AuditId, ct);

        if (audit is null)
        {
            LogAuditMissing(logger, evt.AuditId);
            return;
        }

        var finding = audit.Findings.Single(f => f.Id == evt.FindingId);
        if (finding.NcId is null)
        {
            audit.AcknowledgeFindingNc(evt.FindingId, nc.Id);
            await db.SaveChangesAsync(ct);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Raised {NcRef} for audit finding {AuditRef}/{FindingId}")]
    private static partial void LogNcRaised(ILogger logger, string ncRef, string auditRef, Guid findingId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Audit {AuditId} not found while acknowledging finding NC")]
    private static partial void LogAuditMissing(ILogger logger, Guid auditId);
}
