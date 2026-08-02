using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Operations;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Sla;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Sla;

// â”€â”€ SLA definitions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record UpsertSlaCommand(string Module, string Severity, int TargetHours) : ICommand<Guid>;

public sealed class UpsertSlaValidator : AbstractValidator<UpsertSlaCommand>
{
    public UpsertSlaValidator()
    {
        RuleFor(x => x.Module).NotEmpty();
        RuleFor(x => x.Severity).NotEmpty();
        RuleFor(x => x.TargetHours).GreaterThan(0);
    }
}

public sealed class UpsertSlaHandler(IAppDbContext db) : ICommandHandler<UpsertSlaCommand, Guid>
{
    public async Task<Guid> Handle(UpsertSlaCommand c, CancellationToken ct)
    {
        var module = c.Module.Trim().ToUpperInvariant();
        var severity = c.Severity.Trim().ToUpperInvariant();
        var existing = await db.SlaDefinitions
            .SingleOrDefaultAsync(s => s.Module == module && s.Severity == severity, ct);

        if (existing is not null)
        {
            existing.SetTarget(c.TargetHours);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var def = SlaDefinition.Create(module, severity, c.TargetHours);
        db.SlaDefinitions.Add(def);
        await db.SaveChangesAsync(ct);
        return def.Id;
    }
}

public sealed record GetSlaDefinitionsQuery : IQuery<IReadOnlyList<SlaDefinitionDto>>;

public sealed class GetSlaDefinitionsHandler(IAppDbContext db)
    : IQueryHandler<GetSlaDefinitionsQuery, IReadOnlyList<SlaDefinitionDto>>
{
    public async Task<IReadOnlyList<SlaDefinitionDto>> Handle(GetSlaDefinitionsQuery q, CancellationToken ct) =>
        await db.SlaDefinitions.AsNoTracking().OrderBy(s => s.Module).ThenBy(s => s.Severity)
            .Select(s => new SlaDefinitionDto(s.Id, s.Module, s.Severity, s.TargetHours))
            .ToListAsync(ct);
}

// â”€â”€ Work tasks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record CreateTaskCommand(
    string Subject, string? SubjectRef, Guid? AssigneeUserId, string? AssigneeRole, DateOnly DueDate)
    : ICommand<Guid>;

public sealed class CreateTaskHandler(IAppDbContext db) : ICommandHandler<CreateTaskCommand, Guid>
{
    public async Task<Guid> Handle(CreateTaskCommand c, CancellationToken ct)
    {
        var task = WorkTask.Create(c.Subject, c.SubjectRef, c.AssigneeUserId, c.AssigneeRole, c.DueDate);
        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return task.Id;
    }
}

[RequireInternalActor]
public sealed record CompleteTaskCommand(Guid TaskId) : ICommand;

public sealed class CompleteTaskHandler(IAppDbContext db, IClock clock) : ICommandHandler<CompleteTaskCommand>
{
    public async Task Handle(CompleteTaskCommand c, CancellationToken ct)
    {
        var task = await db.WorkTasks.SingleOrDefaultAsync(t => t.Id == c.TaskId, ct)
            ?? throw new DomainException("TASK-404", "Task not found.");
        task.Complete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// "My Tasks": every task assigned to me directly or to a role I hold — pending
/// first (by due date), then completed work most recent first. Completed tasks
/// stay on the queue deliberately: hiding them made the page go blank the moment
/// a user finished their last task, which read as the page being broken, and it
/// erased the visible record of work done.
/// </summary>
public sealed record GetMyTasksQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<WorkTaskDto>>;

public sealed class GetMyTasksHandler(IAppDbContext db, ICurrentUser user, IUserPrivileges privileges, IClock clock)
    : IQueryHandler<GetMyTasksQuery, Contracts.Common.PagedResponse<WorkTaskDto>>
{
    public async Task<Contracts.Common.PagedResponse<WorkTaskDto>> Handle(GetMyTasksQuery q, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        // A role-routed task must match BOTH role vocabularies, resolved from the
        // database rather than the token: existing rows hold the legacy tier name
        // ("TenantAdmin") while v1.51 roles are tenant-defined ("Tenant
        // Administrator"), and the JWT's tier claim goes stale the moment an
        // administrator reassigns a role mid-session. The tier comes from the
        // user row (id-bound), the tenant-defined name from the per-request
        // privilege resolution.
        var tier = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role.ToString())
            .SingleAsync(ct);
        var dynamicRole = privileges.RoleName ?? string.Empty;

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await db.WorkTasks.AsNoTracking()
            .Where(t => t.AssigneeUserId == userId
                        || t.AssigneeRole == tier
                        || (dynamicRole != "" && t.AssigneeRole == dynamicRole))
            // Explicit ordinal — the column is an enum-as-string, so a bare
            // OrderBy(Status) would sort alphabetically and put Completed first.
            .OrderBy(t => t.Status == WorkTaskStatus.Pending ? 0 : 1)
            .ThenBy(t => t.Status == WorkTaskStatus.Pending ? t.DueDate : today)
            .ThenByDescending(t => t.CompletedAtUtc)
            .Select(t => new WorkTaskDto(
                t.Id, t.Subject, t.SubjectRef, t.AssigneeUserId, t.AssigneeRole,
                t.DueDate, t.Status.ToString(),
                t.Status == WorkTaskStatus.Pending && t.DueDate < today))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

// â”€â”€ Escalation policies (arm/cancel timers off NC/CAPA events) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Arms an escalation timer when a CAPA action is planned (deadline = its due date).</summary>
public sealed class ArmEscalationOnCapaPlannedPolicy(IAppDbContext db, ICurrentTenantSetter tenantSetter)
    : INotificationHandler<DomainEventNotification<CapaActionPlanned>>
{
    public async Task Handle(DomainEventNotification<CapaActionPlanned> n, CancellationToken ct)
    {
        var e = n.Event;
        var tenantId = await db.Nonconformances.IgnoreQueryFilters()
            .Where(x => x.Id == e.NcId).Select(x => x.TenantId).SingleAsync(ct);
        tenantSetter.Set(tenantId);

        var subjectRef = $"CAPA:{e.ActionId:N}";
        if (await db.EscalationTimers.AnyAsync(t => t.SubjectRef == subjectRef, ct))
        {
            return;
        }

        var deadline = new DateTimeOffset(e.DueDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var timer = EscalationTimer.Arm(subjectRef, e.OwnerId, deadline);
        timer.TenantId = tenantId;
        db.EscalationTimers.Add(timer);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Cancels the CAPA timer when its action completes.</summary>
public sealed class CancelEscalationOnCapaCompletedPolicy(IAppDbContext db, ICurrentTenantSetter tenantSetter)
    : INotificationHandler<DomainEventNotification<CapaActionCompleted>>
{
    public async Task Handle(DomainEventNotification<CapaActionCompleted> n, CancellationToken ct)
    {
        var tenantId = await db.Nonconformances.IgnoreQueryFilters()
            .Where(x => x.Id == n.Event.NcId).Select(x => x.TenantId).SingleAsync(ct);
        tenantSetter.Set(tenantId);

        var subjectRef = $"CAPA:{n.Event.ActionId:N}";
        var timer = await db.EscalationTimers.SingleOrDefaultAsync(t => t.SubjectRef == subjectRef, ct);
        if (timer is { Active: true })
        {
            timer.Cancel();
            await db.SaveChangesAsync(ct);
        }
    }
}
