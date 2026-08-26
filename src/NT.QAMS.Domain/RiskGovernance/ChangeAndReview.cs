using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.RiskGovernance;

public enum ChangeStatus { Proposed, Approved, Rejected, Closed, Reviewed, ImplementedPendingRatification }

/// <summary>
/// Assessed impact level of a change, captured at proposal so approval can be routed by risk
/// (HQMS M18). A High-impact change may not be self-approved (segregation of duties).
/// </summary>
public enum ChangeImpactLevel { Low, Medium, High }

/// <summary>
/// Controlled change request. The load-bearing invariant: a change cannot be
/// approved without a linked risk assessment (risk-based thinking, ISO 9001 6.1).
/// Closed changes are immutable.
/// </summary>
public sealed class ChangeRequest : AggregateRoot, ITenantScoped, IAllocatable
{
    private ChangeRequest()
    {
        ChangeRef = null!;
        Title = null!;
        ImpactAnalysis = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string ChangeRef { get; private set; }
    public string Title { get; private set; }
    public string ImpactAnalysis { get; private set; }
    public ChangeImpactLevel ImpactLevel { get; private set; }
    public Guid ProposedBy { get; private set; }
    public Guid? RiskItemId { get; private set; }
    public ChangeStatus Status { get; private set; }

    // Emergency-change pathway (HQMS M18): an urgent change implemented before formal
    // approval, then ratified retrospectively — with a hard deadline by which the
    // retrospective documentation and risk assessment must be in place.
    public bool IsEmergency { get; private set; }
    public DateOnly? RetrospectiveDeadline { get; private set; }
    public Guid? RatifiedBy { get; private set; }
    public DateTimeOffset? RatifiedAtUtc { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? ImplementationNotes { get; private set; }

    // Post-implementation review (F-11 / ISO 9001 §8.5.6 / EU Annex 11 §10): the
    // verification, after the change is live, that it achieved its purpose without
    // adverse effect. Populated only once the change has been reviewed.
    public Guid? PostImplementationReviewedBy { get; private set; }
    public DateTimeOffset? PostImplementationReviewedAtUtc { get; private set; }
    public bool? ChangeEffective { get; private set; }
    public string? PostImplementationReviewNotes { get; private set; }

    public static ChangeRequest Propose(
        string changeRef, string title, string impactAnalysis, Guid proposedBy,
        ChangeImpactLevel impactLevel = ChangeImpactLevel.Medium)
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
            ImpactLevel = impactLevel,
            ProposedBy = proposedBy,
            Status = ChangeStatus.Proposed,
        };
    }

    /// <summary>
    /// Raises an emergency change (HQMS M18): an urgent change that has already been implemented to
    /// contain a live risk, entered here so it is documented and ratified retrospectively by the
    /// <paramref name="retrospectiveDeadline"/>. Emergency changes are High-impact by nature and go
    /// straight to <see cref="ChangeStatus.ImplementedPendingRatification"/>.
    /// </summary>
    public static ChangeRequest ProposeEmergency(
        string changeRef, string title, string impactAnalysis, Guid proposedBy, DateOnly retrospectiveDeadline)
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
            ImpactLevel = ChangeImpactLevel.High,
            ProposedBy = proposedBy,
            IsEmergency = true,
            RetrospectiveDeadline = retrospectiveDeadline,
            Status = ChangeStatus.ImplementedPendingRatification,
        };
    }

    public void LinkRiskAssessment(Guid riskItemId)
    {
        // A risk assessment may be linked while a change is still Proposed, or retrospectively
        // while an emergency change awaits ratification.
        if (Status is not (ChangeStatus.Proposed or ChangeStatus.ImplementedPendingRatification))
        {
            throw new InvalidStateTransitionException("CHG-010", $"Cannot link a risk assessment to a change in state {Status}.");
        }

        RiskItemId = riskItemId;
    }

    public void Approve(Guid actorId, DateTimeOffset at)
    {
        Require(ChangeStatus.Proposed, "CHG-011", "approve");
        if (RiskItemId is null)
        {
            throw new DomainException("CHG-012", "A change cannot be approved without a linked risk assessment.");
        }

        // Impact-based routing (HQMS M18): a High-impact change cannot be self-approved by its
        // proposer — it requires an independent approver (SoD-CHG-001).
        if (ImpactLevel == ChangeImpactLevel.High && actorId == ProposedBy)
        {
            throw new DomainException("CHG-016", "A high-impact change must be approved by someone other than its proposer.");
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

    /// <summary>
    /// Record the post-implementation review of a closed (implemented) change:
    /// whether it proved effective and the supporting notes. This is the
    /// effectiveness/verification stage the change lifecycle previously lacked
    /// (F-11). A reviewed change is fully terminal and immutable.
    /// </summary>
    public void RecordPostImplementationReview(Guid reviewerId, bool effective, string notes, DateTimeOffset at)
    {
        Require(ChangeStatus.Closed, "CHG-020", "review");
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainException("CHG-021", "Post-implementation review notes are required.");
        }

        Status = ChangeStatus.Reviewed;
        PostImplementationReviewedBy = reviewerId;
        PostImplementationReviewedAtUtc = at;
        ChangeEffective = effective;
        PostImplementationReviewNotes = notes.Trim();
        Raise(new ChangePostImplementationReviewed(Id, ChangeRef, effective, reviewerId, TenantId));
    }

    /// <summary>
    /// Retrospectively ratifies an emergency change (HQMS M18): the credentials committee confirms,
    /// after the fact, that the already-implemented change was justified and its risk assessment is
    /// in place. Requires a linked (retrospective) risk assessment; moves the change to Closed so it
    /// can then pass the normal post-implementation review. A non-emergency change never enters this
    /// state and so can never be ratified.
    /// </summary>
    public void Ratify(Guid actorId, string implementationNotes, DateTimeOffset at)
    {
        Require(ChangeStatus.ImplementedPendingRatification, "CHG-030", "ratify");
        if (RiskItemId is null)
        {
            throw new DomainException("CHG-031", "An emergency change cannot be ratified without a retrospective risk assessment.");
        }

        if (actorId == ProposedBy)
        {
            throw new DomainException("CHG-032", "An emergency change must be ratified by someone other than its proposer.");
        }

        RatifiedBy = actorId;
        RatifiedAtUtc = at;
        ImplementationNotes = implementationNotes?.Trim() ?? string.Empty;
        Status = ChangeStatus.Closed;
        Raise(new ChangeRatified(Id, ChangeRef, actorId, TenantId));
    }

    /// <summary>True when an emergency change has not been ratified by its retrospective deadline.</summary>
    public bool IsRatificationOverdue(DateOnly asOf) =>
        IsEmergency && Status == ChangeStatus.ImplementedPendingRatification
        && RetrospectiveDeadline is { } deadline && asOf > deadline;

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
/// A named attendee of a management review, held as a user identity — not a
/// display string — so the invitation can be delivered to a real mailbox and a
/// later rename in the directory can never orphan the attendance record.
/// </summary>
public sealed class ReviewParticipant : Entity
{
    internal ReviewParticipant(Guid userId) => UserId = userId;

    private ReviewParticipant() { }

    public Guid UserId { get; private set; }
}

/// <summary>
/// Management review. Decisions accumulate while scheduled; closing records the
/// chair and minutes, after which the record is immutable (ISO 9001 9.3).
/// </summary>
public sealed class ManagementReview : AggregateRoot, ITenantScoped, IAllocatable
{
    private readonly List<ReviewDecision> _decisions = [];
    private readonly List<ReviewParticipant> _participantUsers = [];

    private ManagementReview()
    {
        ReviewRef = null!;
        Title = null!;
        Participants = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string ReviewRef { get; private set; }
    public string Title { get; private set; }
    public DateOnly ReviewDate { get; private set; }

    /// <summary>Human-readable attendee list as recorded in the minutes.</summary>
    public string Participants { get; private set; }

    /// <summary>Agenda circulated with the invitation; part of the review record.</summary>
    public string? Agenda { get; private set; }

    /// <summary>Where the meeting happens — supplied by the organiser or generated.</summary>
    public string? MeetingLink { get; private set; }

    public ReviewStatus Status { get; private set; }
    public string? Minutes { get; private set; }
    public Guid? ClosedBy { get; private set; }

    public IReadOnlyList<ReviewDecision> Decisions => _decisions.AsReadOnly();
    public IReadOnlyList<ReviewParticipant> ParticipantUsers => _participantUsers.AsReadOnly();

    public static ManagementReview Schedule(
        string reviewRef, string title, DateOnly reviewDate, string participants,
        string? agenda = null, string? meetingLink = null,
        IReadOnlyCollection<Guid>? participantUserIds = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("MRV-001", "Review title is required.");
        }

        if (meetingLink is not null && !IsValidMeetingLink(meetingLink))
        {
            throw new DomainException(
                "MRV-005", "The meeting link must be an absolute http(s) URL.");
        }

        var review = new ManagementReview
        {
            ReviewRef = reviewRef,
            Title = title.Trim(),
            ReviewDate = reviewDate,
            Participants = participants?.Trim() ?? string.Empty,
            Agenda = string.IsNullOrWhiteSpace(agenda) ? null : agenda.Trim(),
            MeetingLink = meetingLink?.Trim(),
            Status = ReviewStatus.Scheduled,
        };

        foreach (var userId in (participantUserIds ?? []).Distinct())
        {
            review._participantUsers.Add(new ReviewParticipant(userId));
        }

        review.Raise(new ManagementReviewScheduled(
            review.Id, reviewRef, review.Title, reviewDate,
            review.Agenda, review.MeetingLink,
            [.. review._participantUsers.Select(p => p.UserId)]));
        return review;
    }

    /// <summary>An absolute http(s) URL — nothing else may be circulated as a meeting link.</summary>
    private static bool IsValidMeetingLink(string link) =>
        Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

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

public sealed record ChangePostImplementationReviewed(
    Guid ChangeId, string ChangeRef, bool Effective, Guid ReviewedBy, Guid TenantId) : DomainEvent;

public sealed record ChangeRatified(
    Guid ChangeId, string ChangeRef, Guid RatifiedBy, Guid TenantId) : DomainEvent;

public sealed record ReviewClosed(
    Guid ReviewId, string ReviewRef, Guid ClosedBy, int DecisionCount, Guid TenantId) : DomainEvent;

/// <summary>
/// A management review was scheduled — the fact the invitation policy delivers
/// to the named participants. Carries refs only; tenancy is attributed by the
/// outbox from the aggregate, not the payload.
/// </summary>
public sealed record ManagementReviewScheduled(
    Guid ReviewId, string ReviewRef, string Title, DateOnly ReviewDate,
    string? Agenda, string? MeetingLink, Guid[] ParticipantUserIds) : DomainEvent;
