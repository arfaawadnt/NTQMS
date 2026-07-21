using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Improvement;

public enum NcStatus
{
    Draft, Raised, Assigned, Rca, ActionPlan, PendingVerification, EffectivenessCheck, Closed, Rejected,
}

public enum NcSourceType { Internal, Complaint, Audit, Supplier, ProficiencyTest }

public enum CapaActionType { Corrective, Preventive }

public enum CapaActionStatus { Open, Completed }

public enum RcaMethod { FiveWhys, Fishbone, Other }

public sealed class CapaAction : Entity
{
    internal CapaAction(CapaActionType type, string details, Guid ownerId, DateOnly dueDate)
    {
        Type = type;
        Details = details;
        OwnerId = ownerId;
        DueDate = dueDate;
        Status = CapaActionStatus.Open;
    }

    private CapaAction() { Details = null!; }

    public CapaActionType Type { get; private set; }
    public string Details { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateOnly DueDate { get; private set; }
    public CapaActionStatus Status { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    internal void Complete(DateTimeOffset at)
    {
        if (Status == CapaActionStatus.Completed)
        {
            throw new InvalidStateTransitionException("CAPA-002", "Action is already completed.");
        }

        Status = CapaActionStatus.Completed;
        CompletedAtUtc = at;
    }
}

public sealed class RcaRecord : Entity
{
    internal RcaRecord(RcaMethod method, string analysis, Guid investigatorId)
    {
        Method = method;
        Analysis = analysis;
        InvestigatorId = investigatorId;
    }

    private RcaRecord() { Analysis = null!; }

    public RcaMethod Method { get; private set; }
    public string Analysis { get; private set; }
    public Guid InvestigatorId { get; private set; }
}

/// <summary>
/// The Nonconformance aggregate — canonical 9-state machine per the domain model.
/// Invariants enforced here: guarded transitions; cannot submit for verification
/// with open CAPA actions; Rejected only from Raised; SoD rule SOD-CAPA-001
/// (closer ≠ raiser). E-signature envelopes attach to Verify/Close in the full
/// Identity phase; transitions already carry the acting user.
/// </summary>
public sealed class Nonconformance : AggregateRoot, ITenantScoped
{
    private readonly List<CapaAction> _capaActions = [];
    private readonly List<RcaRecord> _rcaRecords = [];

    private Nonconformance()
    {
        NcRef = null!;
        Title = null!;
        Description = null!;
    }

    public Guid TenantId { get; set; }
    public string NcRef { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public int Severity { get; private set; }
    public int Likelihood { get; private set; }
    public int Rpn { get; private set; }
    public NcSourceType SourceType { get; private set; }
    /// <summary>Logical origin ref (e.g. "AUD-2026-0001#findingId") — idempotency key for source-driven NCs.</summary>
    public string? SourceRef { get; private set; }
    public NcStatus Status { get; private set; }
    public Guid RaisedBy { get; private set; }
    public Guid? AssignedTo { get; private set; }
    public string? RejectionReason { get; private set; }

    public IReadOnlyList<CapaAction> CapaActions => _capaActions.AsReadOnly();
    public IReadOnlyList<RcaRecord> RcaRecords => _rcaRecords.AsReadOnly();

    public static Nonconformance Raise(
        string ncRef, string title, string description,
        int severity, int likelihood, NcSourceType sourceType, Guid raisedBy,
        string? sourceRef = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("NC-001", "Title is required.");
        }

        if (severity is < 1 or > 5 || likelihood is < 1 or > 5)
        {
            throw new DomainException("NC-002", "Severity and likelihood must each be 1-5 and explicitly assessed.");
        }

        var nc = new Nonconformance
        {
            NcRef = ncRef,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Severity = severity,
            Likelihood = likelihood,
            Rpn = severity * likelihood,
            SourceType = sourceType,
            SourceRef = string.IsNullOrWhiteSpace(sourceRef) ? null : sourceRef.Trim(),
            Status = NcStatus.Draft,
            RaisedBy = raisedBy,
        };
        return nc;
    }

    public void Submit()
    {
        Require(NcStatus.Draft, "NC-010", "submit");
        Status = NcStatus.Raised;
        Raise(new NcRaised(Id, NcRef, Title, Severity, Rpn));
    }

    public void Triage(Guid assigneeId)
    {
        Require(NcStatus.Raised, "NC-011", "triage");
        AssignedTo = assigneeId;
        Status = NcStatus.Assigned;
        Raise(new NcTriaged(Id, NcRef, assigneeId));
    }

    public void Reject(string reason)
    {
        Require(NcStatus.Raised, "NC-012", "reject");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("NC-013", "A rejection reason is required.");
        }

        RejectionReason = reason.Trim();
        Status = NcStatus.Rejected;
        Raise(new NcRejected(Id, NcRef, RejectionReason));
    }

    public void RecordRca(RcaMethod method, string analysis, Guid investigatorId)
    {
        if (Status is not (NcStatus.Assigned or NcStatus.Rca))
        {
            throw new InvalidStateTransitionException("NC-014", $"Cannot record RCA in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(analysis))
        {
            throw new DomainException("NC-015", "RCA analysis text is required.");
        }

        _rcaRecords.Add(new RcaRecord(method, analysis.Trim(), investigatorId));
        Status = NcStatus.Rca;
    }

    public Guid PlanCapaAction(CapaActionType type, string details, Guid ownerId, DateOnly dueDate)
    {
        if (Status is not (NcStatus.Rca or NcStatus.ActionPlan))
        {
            throw new InvalidStateTransitionException("NC-016", $"Cannot plan CAPA actions in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(details))
        {
            throw new DomainException("NC-017", "Action details are required.");
        }

        var action = new CapaAction(type, details.Trim(), ownerId, dueDate);
        _capaActions.Add(action);
        Status = NcStatus.ActionPlan;
        Raise(new CapaActionPlanned(Id, NcRef, action.Id, ownerId, dueDate));
        return action.Id;
    }

    public void CompleteCapaAction(Guid actionId, DateTimeOffset at)
    {
        var action = _capaActions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new DomainException("CAPA-001", "CAPA action not found on this nonconformance.");
        action.Complete(at);
        Raise(new CapaActionCompleted(Id, NcRef, actionId));
    }

    public void SubmitForVerification()
    {
        Require(NcStatus.ActionPlan, "NC-018", "submit for verification");
        if (_capaActions.Count == 0)
        {
            throw new DomainException("NC-019", "At least one CAPA action is required before verification.");
        }

        if (_capaActions.Any(a => a.Status != CapaActionStatus.Completed))
        {
            throw new DomainException("NC-020", "All CAPA actions must be completed before verification.");
        }

        Status = NcStatus.PendingVerification;
    }

    public void Verify(bool passed)
    {
        Require(NcStatus.PendingVerification, "NC-021", "verify");
        Status = passed ? NcStatus.EffectivenessCheck : NcStatus.ActionPlan;
        if (passed)
        {
            Raise(new NcVerified(Id, NcRef));
        }
    }

    /// <summary>Effective ⇒ Closed (SoD: closer ≠ raiser). Not effective ⇒ back to ActionPlan.</summary>
    public void ConfirmEffectiveness(bool effective, Guid actorId)
    {
        Require(NcStatus.EffectivenessCheck, "NC-022", "confirm effectiveness");

        if (!effective)
        {
            Status = NcStatus.ActionPlan;
            return;
        }

        if (actorId == RaisedBy)
        {
            throw new DomainException("SOD-CAPA-001", "Segregation of duties: the raiser cannot close their own nonconformance.");
        }

        Status = NcStatus.Closed;
        Raise(new NcClosed(Id, NcRef, actorId));
    }

    private void Require(NcStatus expected, string code, string action)
    {
        if (Status != expected)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} a nonconformance in state {Status}.");
        }
    }
}

public sealed record NcRaised(Guid NcId, string NcRef, string Title, int Severity, int Rpn) : DomainEvent;
public sealed record NcTriaged(Guid NcId, string NcRef, Guid AssigneeId) : DomainEvent;
public sealed record NcRejected(Guid NcId, string NcRef, string Reason) : DomainEvent;
public sealed record CapaActionPlanned(Guid NcId, string NcRef, Guid ActionId, Guid OwnerId, DateOnly DueDate) : DomainEvent;
public sealed record CapaActionCompleted(Guid NcId, string NcRef, Guid ActionId) : DomainEvent;
public sealed record NcVerified(Guid NcId, string NcRef) : DomainEvent;
public sealed record NcClosed(Guid NcId, string NcRef, Guid ClosedBy) : DomainEvent;
