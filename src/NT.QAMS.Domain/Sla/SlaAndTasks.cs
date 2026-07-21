using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Sla;

/// <summary>Target turnaround for a module+severity, in hours. Config the escalation clock reads.</summary>
public sealed class SlaDefinition : AggregateRoot, ITenantScoped
{
    private SlaDefinition() { Module = null!; Severity = null!; }

    public Guid TenantId { get; set; }
    public string Module { get; private set; }
    public string Severity { get; private set; }
    public int TargetHours { get; private set; }

    public static SlaDefinition Create(string module, string severity, int targetHours)
    {
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(severity))
        {
            throw new DomainException("SLA-001", "Module and severity are required.");
        }

        if (targetHours < 1)
        {
            throw new DomainException("SLA-002", "Target hours must be positive.");
        }

        return new SlaDefinition
        {
            Module = module.Trim().ToUpperInvariant(),
            Severity = severity.Trim().ToUpperInvariant(),
            TargetHours = targetHours,
        };
    }

    public void SetTarget(int targetHours)
    {
        if (targetHours < 1)
        {
            throw new DomainException("SLA-002", "Target hours must be positive.");
        }

        TargetHours = targetHours;
    }
}

public enum WorkTaskStatus { Pending, Completed }

/// <summary>
/// A "My Tasks" queue item. Assignable to a specific user or to a role (role
/// tasks show for anyone holding it). Created manually or by policies
/// (escalations, review decisions). Overdue is a derived read concern.
/// </summary>
public sealed class WorkTask : AggregateRoot, ITenantScoped
{
    private WorkTask() { Subject = null!; }

    public Guid TenantId { get; set; }
    public string Subject { get; private set; }
    public string? SubjectRef { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public string? AssigneeRole { get; private set; }
    public DateOnly DueDate { get; private set; }
    public WorkTaskStatus Status { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static WorkTask Create(
        string subject, string? subjectRef, Guid? assigneeUserId, string? assigneeRole, DateOnly dueDate)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new DomainException("TASK-001", "A task subject is required.");
        }

        if (assigneeUserId is null && string.IsNullOrWhiteSpace(assigneeRole))
        {
            throw new DomainException("TASK-002", "A task must be assigned to a user or a role.");
        }

        return new WorkTask
        {
            Subject = subject.Trim(),
            SubjectRef = subjectRef,
            AssigneeUserId = assigneeUserId,
            AssigneeRole = string.IsNullOrWhiteSpace(assigneeRole) ? null : assigneeRole.Trim(),
            DueDate = dueDate,
            Status = WorkTaskStatus.Pending,
        };
    }

    public void Complete(DateTimeOffset at)
    {
        if (Status == WorkTaskStatus.Completed)
        {
            throw new InvalidStateTransitionException("TASK-003", "Task is already completed.");
        }

        Status = WorkTaskStatus.Completed;
        CompletedAtUtc = at;
    }
}

/// <summary>
/// Escalation timer for an overdue-sensitive subject (e.g. a CAPA action).
/// Armed with a deadline and owner; the tick advances it through the ladder
/// (level 1 at +24h → owner, level 2 at +48h → QM, level 3 at +72h → QM),
/// raising EscalationTriggered at each step. Cancelled when the subject closes.
/// </summary>
public sealed class EscalationTimer : AggregateRoot, ITenantScoped
{
    public const string EscalationRole = "QualityManager";

    private EscalationTimer() { SubjectRef = null!; }

    public Guid TenantId { get; set; }
    public string SubjectRef { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public DateTimeOffset Deadline { get; private set; }
    public int Level { get; private set; }
    public DateTimeOffset? NextStepAtUtc { get; private set; }
    public bool Active { get; private set; }

    public static EscalationTimer Arm(string subjectRef, Guid ownerUserId, DateTimeOffset deadline)
    {
        return new EscalationTimer
        {
            SubjectRef = subjectRef,
            OwnerUserId = ownerUserId,
            Deadline = deadline,
            Level = 0,
            NextStepAtUtc = deadline.AddHours(24),
            Active = true,
        };
    }

    public void Cancel() => Active = false;

    /// <summary>Advances one ladder step if due. Idempotent w.r.t. time — no-op when not yet due or terminal.</summary>
    public void AdvanceIfDue(DateTimeOffset now)
    {
        if (!Active || NextStepAtUtc is null || NextStepAtUtc > now || Level >= 3)
        {
            return;
        }

        Level++;
        var (recipientRole, assignee) = Level switch
        {
            1 => ((string?)null, (Guid?)OwnerUserId), // remind the owner
            _ => (EscalationRole, (Guid?)null),        // escalate to QM
        };

        NextStepAtUtc = Level >= 3 ? null : Deadline.AddHours(24L * (Level + 1));

        Raise(new EscalationTriggered(Id, SubjectRef, Level, assignee, recipientRole, TenantId));
    }
}

public sealed record EscalationTriggered(
    Guid TimerId, string SubjectRef, int Level, Guid? AssigneeUserId, string? RecipientRole, Guid TenantId)
    : DomainEvent;
