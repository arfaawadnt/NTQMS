using MediatR;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Sla;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.Sla;

/// <summary>
/// On each escalation step, create a work-task for the target (owner at level 1,
/// QM role above). Idempotent by (subjectRef, level) so redelivery never
/// duplicates a task. The notification alert is handled separately by the
/// notification policies subscribing to the same event.
/// </summary>
public sealed class EscalationToTaskPolicy(IAppDbContext db, ICurrentTenantSetter tenantSetter, IClock clock)
    : INotificationHandler<DomainEventNotification<EscalationTriggered>>
{
    public async Task Handle(DomainEventNotification<EscalationTriggered> n, CancellationToken ct)
    {
        var e = n.Event;
        tenantSetter.Set(e.TenantId);

        var subjectRef = $"{e.SubjectRef}#L{e.Level}";
        if (await db.WorkTasks.AnyAsync(t => t.SubjectRef == subjectRef, ct))
        {
            return;
        }

        var task = WorkTask.Create(
            $"Escalation L{e.Level}: {e.SubjectRef} is overdue",
            subjectRef,
            e.AssigneeUserId,
            e.RecipientRole,
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        task.TenantId = e.TenantId;
        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(ct);
    }
}
