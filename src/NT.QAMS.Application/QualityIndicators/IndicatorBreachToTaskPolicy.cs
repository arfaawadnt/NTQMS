using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.QualityIndicators;
using NT.QAMS.Domain.Sla;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.QualityIndicators;

/// <summary>
/// Breach workflow (HQMS M06): an indicator value that crosses its action threshold
/// automatically opens an analysis task for the quality function, so measurement and
/// action are connected rather than a breach sitting unseen on a dashboard. Runs from
/// the outbox; idempotent by SubjectRef "INDBREACH:{code}:{period}" against open tasks;
/// sets the tenant context because it executes in a background scope.
/// </summary>
public sealed partial class IndicatorBreachToTaskPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IClock clock,
    ILogger<IndicatorBreachToTaskPolicy> logger)
    : INotificationHandler<DomainEventNotification<IndicatorBreached>>
{
    public async Task Handle(DomainEventNotification<IndicatorBreached> notification, CancellationToken ct)
    {
        var e = notification.Event;

        var tenantId = await db.QualityIndicators.IgnoreQueryFilters()
            .Where(i => i.Id == e.IndicatorId)
            .Select(i => i.TenantId)
            .SingleAsync(ct);
        tenantSetter.Set(tenantId);

        var subjectRef = $"INDBREACH:{e.Code}:{e.Period:yyyy-MM-dd}";
        var alreadyOpen = await db.WorkTasks.IgnoreQueryFilters().AnyAsync(
            t => t.TenantId == tenantId && t.SubjectRef == subjectRef && t.Status == WorkTaskStatus.Pending, ct);
        if (alreadyOpen)
        {
            return;
        }

        var value = e.Value.ToString("0.####", CultureInfo.InvariantCulture);
        var task = WorkTask.Create(
            $"Indicator breach: {e.Code} = {value} for {e.Period:yyyy-MM} — analyse and action",
            subjectRef,
            assigneeUserId: null,
            assigneeRole: "QualityManager",
            dueDate: DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(14));
        task.TenantId = tenantId;
        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(ct);
        LogBreachTask(logger, e.Code, e.Period);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Analysis task opened for breached indicator {Code} in {Period}")]
    private static partial void LogBreachTask(ILogger logger, string code, DateOnly period);
}
