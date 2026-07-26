using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum PtPlanStatus { Draft, Approved, Closed }

/// <summary>
/// One planned scheme/analyte line of the annual plan: how many cycles are
/// committed and how many were actually fulfilled (linked to enrollments as
/// they complete).
/// </summary>
public sealed class PtPlanItem : Entity
{
    internal PtPlanItem(string scheme, string analyte, string? provider, int plannedCycles, string? notes)
    {
        Scheme = scheme;
        Analyte = analyte;
        Provider = provider;
        PlannedCycles = plannedCycles;
        Notes = notes;
    }

    private PtPlanItem() { Scheme = null!; Analyte = null!; }

    public string Scheme { get; private set; }
    public string Analyte { get; private set; }
    public string? Provider { get; private set; }
    public int PlannedCycles { get; private set; }
    public int FulfilledCycles { get; private set; }
    /// <summary>The most recent enrollment counted against this line.</summary>
    public string? LastEnrollmentRef { get; private set; }
    public string? Notes { get; private set; }

    internal void RecordFulfilment(string enrollmentRef)
    {
        FulfilledCycles++;
        LastEnrollmentRef = enrollmentRef;
    }
}

/// <summary>
/// Annual PT/EQA participation plan (ISO 17025 §7.7.2 / ISO 15189 §7.3.7.3):
/// the lab commits, per scheme and analyte, to a number of cycles for the
/// year. Lines are edited in Draft, frozen by QM approval, and fulfilment is
/// recorded against the approved plan as enrollments complete — so coverage
/// (planned vs fulfilled) is honest at year end. Closing captures the final
/// coverage; unfulfilled lines stay visible as gaps.
/// </summary>
public sealed class PtPlan : AggregateRoot, ITenantScoped
{
    private readonly List<PtPlanItem> _items = [];

    private PtPlan() { PlanRef = null!; }

    public Guid TenantId { get; set; }
    public string PlanRef { get; private set; }
    public int Year { get; private set; }
    public PtPlanStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? ClosureSummary { get; private set; }

    public IReadOnlyList<PtPlanItem> Items => _items.AsReadOnly();

    public static PtPlan Create(string planRef, int year)
    {
        if (year is < 2000 or > 2100)
        {
            throw new DomainException("PTP-001", "A plausible plan year is required.");
        }

        return new PtPlan { PlanRef = planRef, Year = year, Status = PtPlanStatus.Draft };
    }

    public Guid AddItem(string scheme, string analyte, string? provider, int plannedCycles, string? notes)
    {
        RequireDraft();
        if (string.IsNullOrWhiteSpace(scheme) || string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("PTP-002", "Scheme and analyte are required for a plan line.");
        }

        if (plannedCycles < 1)
        {
            throw new DomainException("PTP-003", "At least one cycle must be planned per line.");
        }

        var item = new PtPlanItem(
            scheme.Trim(), analyte.Trim(),
            string.IsNullOrWhiteSpace(provider) ? null : provider.Trim(),
            plannedCycles,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
        _items.Add(item);
        return item.Id;
    }

    public void RemoveItem(Guid itemId)
    {
        RequireDraft();
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("PTP-404", "Plan line not found.");
        _items.Remove(item);
    }

    public void Approve(Guid actorId, DateTimeOffset at)
    {
        EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001");
        if (Status != PtPlanStatus.Draft)
        {
            throw new InvalidStateTransitionException("PTP-010", $"Only a draft plan can be approved (current: {Status}).");
        }

        if (_items.Count == 0)
        {
            throw new DomainException("PTP-011", "An empty plan cannot be approved — add at least one scheme/analyte line.");
        }

        Status = PtPlanStatus.Approved;
        ApprovedBy = actorId;
        ApprovedAtUtc = at;
    }

    /// <summary>Counts a completed enrollment against the matching plan line (scheme + analyte).</summary>
    public void RecordFulfilment(Guid itemId, string enrollmentRef)
    {
        if (Status != PtPlanStatus.Approved)
        {
            throw new InvalidStateTransitionException("PTP-012", $"Fulfilment is recorded against an approved plan (current: {Status}).");
        }

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("PTP-404", "Plan line not found.");
        item.RecordFulfilment(enrollmentRef);
    }

    public void Close(string closureSummary)
    {
        if (Status != PtPlanStatus.Approved)
        {
            throw new InvalidStateTransitionException("PTP-013", $"Only an approved plan can be closed (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(closureSummary))
        {
            throw new DomainException("PTP-014", "A closure summary (coverage and gaps) is required.");
        }

        Status = PtPlanStatus.Closed;
        ClosureSummary = closureSummary.Trim();
    }

    private void RequireDraft()
    {
        if (Status != PtPlanStatus.Draft)
        {
            throw new InvalidStateTransitionException("PTP-015", "The approved plan is frozen — fulfilment is recorded instead of editing lines.");
        }
    }
}
