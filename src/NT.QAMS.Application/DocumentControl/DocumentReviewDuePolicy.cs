using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Notifications;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Sla;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.DocumentControl;

/// <summary>
/// Periodic-review saga (ISO 17025 §8.3): when the sweep flags a published
/// document's review as due, notify per the DOC_REVIEW_DUE rule and open a
/// Quality Manager work task with a 30-day window. Idempotent by
/// SubjectRef "DOCREV:{code}" against open tasks; runs in a background scope,
/// so the tenant context is set from the document row.
/// </summary>
public sealed partial class DocumentReviewDuePolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    NotificationDispatcher dispatcher,
    IClock clock,
    ILogger<DocumentReviewDuePolicy> logger)
    : INotificationHandler<DomainEventNotification<DocumentReviewDue>>
{
    public async Task Handle(DomainEventNotification<DocumentReviewDue> notification, CancellationToken ct)
    {
        var e = notification.Event;
        var tenantId = await db.Documents.IgnoreQueryFilters()
            .Where(d => d.Id == e.DocumentId).Select(d => d.TenantId).SingleAsync(ct);
        tenantSetter.Set(tenantId);

        await dispatcher.DispatchAsync(e.EventId, tenantId, NotificationEventPolicies.DocumentReviewDueKey,
            new Dictionary<string, string>
            {
                ["ref"] = e.Code, ["title"] = e.Title, ["due"] = e.DueOn.ToString("yyyy-MM-dd"),
            }, ct);

        var subjectRef = $"DOCREV:{e.Code}";
        var alreadyOpen = await db.WorkTasks.IgnoreQueryFilters().AnyAsync(
            t => t.TenantId == tenantId && t.SubjectRef == subjectRef && t.Status == WorkTaskStatus.Pending, ct);
        if (alreadyOpen)
        {
            return;
        }

        var task = WorkTask.Create(
            $"Periodic review due: {e.Code} — {e.Title}",
            subjectRef,
            assigneeUserId: null,
            assigneeRole: "QualityManager",
            dueDate: DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(30));
        task.TenantId = tenantId;
        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(ct);
        LogReviewTask(logger, e.Code);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Periodic-review task opened for {Code}")]
    private static partial void LogReviewTask(ILogger logger, string code);
}
