using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.RiskGovernance;

public enum ChangeStatus { Proposed, Approved, Rejected, Closed }

/// <summary>
/// Controlled change request. The load-bearing invariant: a change cannot be
/// approved without a linked risk assessment (risk-based thinking, ISO 9001 6.1).
/// Closed changes are immutable.
/// </summary>
public sealed class ChangeRequest : AggregateRoot, ITenantScoped
{
    private ChangeRequest()
    {
        ChangeRef = null!;
        Title = null!;
        ImpactAnalysis = null!;
    }

    public Guid TenantId { get; set; }
    public string ChangeRef { get; private set; }
    public string Title { get; private set; }
    public string ImpactAnalysis { get; private set; }
    public Guid ProposedBy { get; private set; }
    public Guid? RiskItemId { get; private set; }
    public ChangeStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? ImplementationNotes { get; private set; }

    public static ChangeRequest Propose(string changeRef, string title, string impactAnalysis, Guid proposedBy)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("CHG-001", "Change title is required.");
        }

        if (string.IsNullOrWhiteSpace(impactAnalysis))
        {
            throw new DomainException("CHG-002", "An impact analysis is required to propose a change.");
        }

        return new ChangeRequest
        {
            ChangeRef = changeRef,
            Title = title.Trim(),
            ImpactAnalysis = impactAnalysis.Trim(),
            ProposedBy = proposedBy,
            Status = ChangeStatus.Proposed,
        };
    }

    public void LinkRiskAssessment(Guid riskItemId)
    {
        Require(ChangeStatus.Proposed, "CHG-010", "link a risk assessment to");
        RiskItemId = riskItemId;
    }

    public void Approve(Guid actorId, DateTimeOffset at)
    {
        Require(ChangeStatus.Proposed, "CHG-011", "approve");
        if (RiskItemId is null)
        {
            throw new DomainException("CHG-012", "A change cannot be approved without a linked risk assessment.");
        }

        Status = ChangeStatus.Approved;
        ApprovedBy = actorId;
        ApprovedAtUtc = at;
        Raise(new ChangeApproved(Id, ChangeRef, Title, actorId, TenantId));
    }

    public void Reject(string reason)
    {
        Require(ChangeStatus.Proposed, "CHG-013", "reject");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("CHG-014", "A rejection reason is required.");
        }

        Status = ChangeStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    public void Close(string implementationNotes)
    {
        Require(ChangeStatus.Approved, "CHG-015", "close");
        ImplementationNotes = implementationNotes?.Trim() ?? string.Empty;
        Status = ChangeStatus.Closed;
    }

    private void Require(ChangeStatus expected, string code, string action)
    {
        if (Status != expected)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} a change in state {Status}.");
        }
    }
}

public enum ReviewStatus { Scheduled, Closed }

public sealed class ReviewDecision : Entity
{
    internal ReviewDecision(string description, Guid ownerId, DateOnly dueDate)
    {
        Description = description;
        OwnerId = ownerId;
        DueDate = dueDate;
    }

    private ReviewDecision() { Description = null!; }

    public string Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateOnly DueDate { get; private set; }
}

/// <summary>
/// Management review. Decisions accumulate while scheduled; closing records the
/// chair and minutes, after which the record is immutable (ISO 9001 9.3).
/// </summary>
public sealed class ManagementReview : AggregateRoot, ITenantScoped
{
    private readonly List<ReviewDecision> _decisions = [];

    private ManagementReview()
    {
        ReviewRef = null!;
        Title = null!;
        Participants = null!;
    }

    public Guid TenantId { get; set; }
    public string ReviewRef { get; private set; }
    public string Title { get; private set; }
    public DateOnly ReviewDate { get; private set; }
    public string Participants { get; private set; }
    public ReviewStatus Status { get; private set; }
    public string? Minutes { get; private set; }
    public Guid? ClosedBy { get; private set; }

    public IReadOnlyList<ReviewDecision> Decisions => _decisions.AsReadOnly();

    public static ManagementReview Schedule(
        string reviewRef, string title, DateOnly reviewDate, string participants)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("MRV-001", "Review title is required.");
        }

        return new ManagementReview
        {
            ReviewRef = reviewRef,
            Title = title.Trim(),
            ReviewDate = reviewDate,
            Participants = participants?.Trim() ?? string.Empty,
            Status = ReviewStatus.Scheduled,
        };
    }

    public Guid AddDecision(string description, Guid ownerId, DateOnly dueDate)
    {
        RequireScheduled("add decisions to");
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("MRV-002", "Decision description is required.");
        }

        var decision = new ReviewDecision(description.Trim(), ownerId, dueDate);
        _decisions.Add(decision);
        return decision.Id;
    }

    public void Close(Guid chairId, string minutes)
    {
        RequireScheduled("close");
        if (string.IsNullOrWhiteSpace(minutes))
        {
            throw new DomainException("MRV-003", "Minutes are required to close a management review.");
        }

        Status = ReviewStatus.Closed;
        ClosedBy = chairId;
        Minutes = minutes.Trim();
        Raise(new ReviewClosed(Id, ReviewRef, chairId, _decisions.Count, TenantId));
    }

    private void RequireScheduled(string action)
    {
        if (Status != ReviewStatus.Scheduled)
        {
            throw new InvalidStateTransitionException(
                "MRV-004", $"Cannot {action} a review in state {Status} — closed minutes are immutable.");
        }
    }
}

public sealed record ChangeApproved(
    Guid ChangeId, string ChangeRef, string Title, Guid ApprovedBy, Guid TenantId) : DomainEvent;

public sealed record ReviewClosed(
    Guid ReviewId, string ReviewRef, Guid ClosedBy, int DecisionCount, Guid TenantId) : DomainEvent;
