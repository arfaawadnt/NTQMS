using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.Application.Improvement;

/// <summary>
/// Improvement-context saga per the domain model: a complaint validated as
/// justified must open a Nonconformance (source = Complaint), and the NC id is
/// back-linked onto the complaint so the closure gate (CMP-020) can hold.
/// Runs from the outbox — idempotent by SourceRef "CMP:{ref}"; sets the tenant
/// context because it executes in a background scope.
/// </summary>
public sealed partial class ComplaintToNcPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IReferenceNumberGenerator refs,
    ILogger<ComplaintToNcPolicy> logger)
    : INotificationHandler<DomainEventNotification<ComplaintValidated>>
{
    public async Task Handle(DomainEventNotification<ComplaintValidated> notification, CancellationToken ct)
    {
        var e = notification.Event;
        tenantSetter.Set(e.TenantId);

        var sourceRef = $"CMP:{e.ComplaintRef}";
        var existing = await db.Nonconformances
            .FirstOrDefaultAsync(n => n.SourceRef == sourceRef, ct);

        var complaint = await db.Complaints.FirstOrDefaultAsync(c => c.Id == e.ComplaintId, ct);

        if (existing is not null)
        {
            complaint?.LinkNc(existing.Id); // Heal a partially applied earlier run.
            await db.SaveChangesAsync(ct);
            return;
        }

        var ncRef = await refs.NextAsync(e.TenantId, "NC", ct);
        var nc = Nonconformance.Raise(
            ncRef,
            $"Justified complaint {e.ComplaintRef}: {e.Subject}",
            e.Description,
            severity: 3,
            likelihood: 3,
            NcSourceType.Complaint,
            e.LoggedBy,
            sourceRef);
        nc.TenantId = e.TenantId;
        nc.Submit();

        db.Nonconformances.Add(nc);
        complaint?.LinkNc(nc.Id);
        await db.SaveChangesAsync(ct);
        LogNcRaised(logger, nc.NcRef, e.ComplaintRef);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Raised {NcRef} for justified complaint {ComplaintRef}")]
    private static partial void LogNcRaised(ILogger logger, string ncRef, string complaintRef);
}
