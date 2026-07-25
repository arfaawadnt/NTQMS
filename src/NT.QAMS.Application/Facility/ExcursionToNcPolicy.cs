using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Facility;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.Application.Facility;

/// <summary>
/// §6.3 saga: an environmental excursion invalidates the assumption that work
/// performed under those conditions was acceptable, so it must open a
/// Nonconformance for impact assessment (stored samples, in-flight runs,
/// reagent integrity). Runs from the outbox — idempotent by SourceRef
/// "ENV:{readingId}"; sets the tenant context because it executes in a
/// background scope.
/// </summary>
public sealed partial class ExcursionToNcPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IReferenceNumberGenerator refs,
    ILogger<ExcursionToNcPolicy> logger)
    : INotificationHandler<DomainEventNotification<EnvironmentalExcursionDetected>>
{
    public async Task Handle(DomainEventNotification<EnvironmentalExcursionDetected> notification, CancellationToken ct)
    {
        var e = notification.Event;
        tenantSetter.Set(e.TenantId);

        var sourceRef = $"ENV:{e.ReadingId}";
        if (await db.Nonconformances.AnyAsync(n => n.SourceRef == sourceRef, ct))
        {
            return; // Outbox redelivery — the NC already exists.
        }

        var window = e.LowLimit is not null && e.HighLimit is not null
            ? $"{e.LowLimit}–{e.HighLimit} {e.Unit}"
            : e.LowLimit is not null ? $"≥ {e.LowLimit} {e.Unit}" : $"≤ {e.HighLimit} {e.Unit}";

        var ncRef = await refs.NextAsync(e.TenantId, "NC", ct);
        var nc = Nonconformance.Raise(
            ncRef,
            $"Environmental excursion at {e.PointRef} — {e.Name}",
            $"{e.Parameter} read {e.Value} {e.Unit} against the acceptance window {window}. " +
            "Assess the impact on samples, reagents and results exposed to the excursion (ISO 17025 §7.10).",
            severity: 4,
            likelihood: 3,
            NcSourceType.Internal,
            e.RecordedById,
            sourceRef);
        nc.TenantId = e.TenantId;
        nc.Submit();

        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync(ct);
        LogNcRaised(logger, nc.NcRef, e.PointRef);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Raised {NcRef} for environmental excursion at {PointRef}")]
    private static partial void LogNcRaised(ILogger logger, string ncRef, string pointRef);
}
