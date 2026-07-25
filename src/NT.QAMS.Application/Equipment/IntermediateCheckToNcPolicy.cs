using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.Application.Equipment;

/// <summary>
/// §6.4.10 saga: a failed intermediate check casts doubt on every result the
/// instrument produced since its last good check, so it must open a
/// Nonconformance for impact assessment. Runs from the outbox — idempotent by
/// SourceRef "CHK:{checkId}"; sets the tenant context because it executes in a
/// background scope.
/// </summary>
public sealed partial class IntermediateCheckToNcPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IReferenceNumberGenerator refs,
    ILogger<IntermediateCheckToNcPolicy> logger)
    : INotificationHandler<DomainEventNotification<IntermediateCheckFailed>>
{
    public async Task Handle(DomainEventNotification<IntermediateCheckFailed> notification, CancellationToken ct)
    {
        var e = notification.Event;
        tenantSetter.Set(e.TenantId);

        var sourceRef = $"CHK:{e.CheckId}";
        if (await db.Nonconformances.AnyAsync(n => n.SourceRef == sourceRef, ct))
        {
            return; // Outbox redelivery — the NC already exists.
        }

        var ncRef = await refs.NextAsync(e.TenantId, "NC", ct);
        var nc = Nonconformance.Raise(
            ncRef,
            $"Failed intermediate check on {e.Code} — {e.Name}",
            $"The '{e.CheckType}' intermediate check performed on {e.PerformedOn:yyyy-MM-dd} failed. " +
            "Results produced since the last passing check must be assessed for validity (ISO 17025 §7.10).",
            severity: 4,
            likelihood: 3,
            NcSourceType.Internal,
            e.PerformedById,
            sourceRef);
        nc.TenantId = e.TenantId;
        nc.Submit();

        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync(ct);
        LogNcRaised(logger, nc.NcRef, e.Code);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Raised {NcRef} for failed intermediate check on {Code}")]
    private static partial void LogNcRaised(ILogger logger, string ncRef, string code);
}
