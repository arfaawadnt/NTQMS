using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.Application.AnalyticalQuality;

/// <summary>
/// Cross-module saga (same shape as audit findings): an unsatisfactory PT result
/// automatically raises a Nonconformance (source = ProficiencyTest). Runs from
/// the outbox, so idempotent by SourceRef "PT:{ptRef}"; runs in a background
/// scope, so it sets the tenant context from the event.
/// </summary>
public sealed partial class PtToNcPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IReferenceNumberGenerator refs,
    ILogger<PtToNcPolicy> logger)
    : INotificationHandler<DomainEventNotification<PtUnsatisfactory>>
{
    public async Task Handle(DomainEventNotification<PtUnsatisfactory> notification, CancellationToken ct)
    {
        var e = notification.Event;
        tenantSetter.Set(e.TenantId);

        var sourceRef = $"PT:{e.PtRef}";
        if (await db.Nonconformances.AnyAsync(n => n.SourceRef == sourceRef, ct))
        {
            return; // Already raised — idempotent.
        }

        var ncRef = await refs.NextAsync(e.TenantId, "NC", ct);
        var nc = Nonconformance.Raise(
            ncRef,
            $"Unsatisfactory PT result {e.PtRef} ({e.Analyte})",
            $"Proficiency test {e.PtRef} for {e.Analyte} returned z-score {e.ZScore} (|z| >= 3).",
            severity: 4,
            likelihood: 3,
            NcSourceType.ProficiencyTest,
            e.RaisedBy,
            sourceRef);
        nc.TenantId = e.TenantId;
        nc.Submit();

        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync(ct);
        LogNcRaised(logger, nc.NcRef, e.PtRef);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Raised {NcRef} for unsatisfactory PT {PtRef}")]
    private static partial void LogNcRaised(ILogger logger, string ncRef, string ptRef);
}
