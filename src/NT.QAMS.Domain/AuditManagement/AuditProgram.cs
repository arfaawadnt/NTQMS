using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AuditManagement;

/// <summary>Lifecycle of an annual audit programme.</summary>
public enum AuditProgramStatus { Draft, Active, Closed }

/// <summary>Risk-based priority that drives scheduling order within the programme.</summary>
public enum PlannedAuditPriority { Low, Medium, High }

/// <summary>Where a planned audit sits: on the plan, scheduled as a real audit, or completed.</summary>
public enum PlannedAuditStatus { Planned, Scheduled, Completed }

/// <summary>
/// One line of the annual plan: an area to be audited within the cycle. Its scope may be a
/// department and/or a standards chapter (from an M07 set), it carries a risk-based priority
/// and a target quarter, and it is linked to the real audit once one is scheduled — which is
/// how coverage is measured.
/// </summary>
public sealed class PlannedAudit : Entity
{
    internal PlannedAudit(
        string scopeArea, Guid? departmentId, string? standardChapter,
        PlannedAuditPriority priority, int plannedQuarter)
    {
        ScopeArea = scopeArea;
        DepartmentId = departmentId;
        StandardChapter = standardChapter;
        Priority = priority;
        PlannedQuarter = plannedQuarter;
        Status = PlannedAuditStatus.Planned;
    }

    private PlannedAudit() { ScopeArea = null!; }

    public string ScopeArea { get; private set; }
    public Guid? DepartmentId { get; private set; }

    /// <summary>Optional standards-chapter reference this audit covers (e.g. a GAHAR chapter code).</summary>
    public string? StandardChapter { get; private set; }

    public PlannedAuditPriority Priority { get; private set; }

    /// <summary>Target quarter, 1–4.</summary>
    public int PlannedQuarter { get; private set; }

    public PlannedAuditStatus Status { get; private set; }

    /// <summary>The scheduled audit fulfilling this plan line, once one exists.</summary>
    public Guid? ScheduledAuditId { get; private set; }

    public DateOnly? CompletedOn { get; private set; }

    internal void MarkScheduled(Guid auditId)
    {
        if (Status == PlannedAuditStatus.Completed)
        {
            throw new InvalidStateTransitionException(
                "APG-020", "A completed plan line cannot be re-scheduled.");
        }

        ScheduledAuditId = auditId;
        Status = PlannedAuditStatus.Scheduled;
    }

    internal void MarkCompleted(DateOnly on)
    {
        if (Status != PlannedAuditStatus.Scheduled)
        {
            throw new InvalidStateTransitionException(
                "APG-021", "Only a scheduled plan line can be completed.");
        }

        Status = PlannedAuditStatus.Completed;
        CompletedOn = on;
    }
}

/// <summary>
/// The annual audit programme (HQMS M05): a risk-based plan of the audits to run across the
/// cycle so no area goes unaudited. Draft while it is being built, Active once the cycle is
/// running, Closed at year end. Coverage — planned vs scheduled vs completed — is read from
/// the plan lines.
/// </summary>
public sealed class AuditProgram : AggregateRoot, ITenantScoped
{
    private readonly List<PlannedAudit> _plan = [];

    private AuditProgram() { Title = null!; }

    public Guid TenantId { get; set; }
    public int Year { get; private set; }
    public string Title { get; private set; }
    public AuditProgramStatus Status { get; private set; }

    public IReadOnlyList<PlannedAudit> Plan => _plan.AsReadOnly();

    public static AuditProgram Create(int year, string title)
    {
        if (year is < 2000 or > 2100)
        {
            throw new DomainException("APG-001", "A valid programme year is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("APG-002", "A programme title is required.");
        }

        return new AuditProgram { Year = year, Title = title.Trim(), Status = AuditProgramStatus.Draft };
    }

    public Guid AddPlannedAudit(
        string scopeArea, Guid? departmentId, string? standardChapter,
        PlannedAuditPriority priority, int plannedQuarter)
    {
        if (Status == AuditProgramStatus.Closed)
        {
            throw new InvalidStateTransitionException("APG-010", "A closed programme cannot take new plan lines.");
        }

        if (string.IsNullOrWhiteSpace(scopeArea))
        {
            throw new DomainException("APG-011", "A scope area is required.");
        }

        if (plannedQuarter is < 1 or > 4)
        {
            throw new DomainException("APG-012", "The planned quarter must be 1–4.");
        }

        var line = new PlannedAudit(
            scopeArea.Trim(), departmentId,
            string.IsNullOrWhiteSpace(standardChapter) ? null : standardChapter.Trim(),
            priority, plannedQuarter);
        _plan.Add(line);
        return line.Id;
    }

    public void Activate()
    {
        if (Status != AuditProgramStatus.Draft)
        {
            throw new InvalidStateTransitionException("APG-013", $"Cannot activate a programme in state {Status}.");
        }

        if (_plan.Count == 0)
        {
            throw new DomainException("APG-014", "A programme needs at least one planned audit before activation.");
        }

        Status = AuditProgramStatus.Active;
        Raise(new AuditProgramActivated(Id, Year, _plan.Count));
    }

    /// <summary>Links a scheduled audit to a plan line (the plan line's ScopeArea is now covered).</summary>
    public void LinkScheduledAudit(Guid plannedAuditId, Guid auditId)
    {
        RequireActive("APG-015", "schedule against");
        Line(plannedAuditId).MarkScheduled(auditId);
    }

    /// <summary>Marks a plan line completed once its audit is done.</summary>
    public void CompletePlannedAudit(Guid plannedAuditId, DateOnly on)
    {
        RequireActive("APG-016", "complete a line on");
        Line(plannedAuditId).MarkCompleted(on);
    }

    public void Close()
    {
        if (Status != AuditProgramStatus.Active)
        {
            throw new InvalidStateTransitionException("APG-017", $"Cannot close a programme in state {Status}.");
        }

        Status = AuditProgramStatus.Closed;
        Raise(new AuditProgramClosed(Id, Year));
    }

    private PlannedAudit Line(Guid plannedAuditId) =>
        _plan.FirstOrDefault(p => p.Id == plannedAuditId)
        ?? throw new DomainException("APG-018", "Plan line not found in this programme.");

    private void RequireActive(string code, string action)
    {
        if (Status != AuditProgramStatus.Active)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} a programme in state {Status}.");
        }
    }
}

public sealed record AuditProgramActivated(Guid ProgramId, int Year, int PlannedCount) : DomainEvent;
public sealed record AuditProgramClosed(Guid ProgramId, int Year) : DomainEvent;
