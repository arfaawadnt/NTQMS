using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.IdentityAccess;

public enum UserAccessReviewStatus { Open, Completed }

/// <summary>
/// Periodic user-access review / recertification (F-11 / 21 CFR Part 11 §11.10(d),
/// EU Annex 11 §12): on a recurring cadence a named reviewer confirms that every
/// account and its role are still appropriate. Completion snapshots the number of
/// active accounts recertified and records whether any access change was required —
/// evidence of what was actually examined. Completed reviews are immutable.
/// </summary>
public sealed class UserAccessReview : AggregateRoot, ITenantScoped
{
    private UserAccessReview() { ReviewRef = null!; }

    public Guid TenantId { get; set; }
    public string ReviewRef { get; private set; }
    public DateOnly OpenedOn { get; private set; }
    public UserAccessReviewStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    /// <summary>Active accounts recertified at completion — evidence of coverage.</summary>
    public int? AccountsReviewed { get; private set; }
    public bool? ChangesRequired { get; private set; }
    public string? Conclusion { get; private set; }

    public static UserAccessReview Open(string reviewRef, DateOnly openedOn) =>
        new()
        {
            ReviewRef = reviewRef,
            OpenedOn = openedOn,
            Status = UserAccessReviewStatus.Open,
        };

    public void Complete(
        Guid reviewerId, DateTimeOffset at, int accountsReviewed, bool changesRequired, string conclusion)
    {
        if (Status != UserAccessReviewStatus.Open)
        {
            throw new InvalidStateTransitionException("UAR-010", "The access review is already completed and immutable.");
        }

        if (string.IsNullOrWhiteSpace(conclusion))
        {
            throw new DomainException("UAR-011", "A written conclusion is required — 'reviewed' without a statement is not evidence.");
        }

        Status = UserAccessReviewStatus.Completed;
        ReviewedBy = reviewerId;
        CompletedAtUtc = at;
        AccountsReviewed = accountsReviewed;
        ChangesRequired = changesRequired;
        Conclusion = conclusion.Trim();
        Raise(new UserAccessReviewCompleted(Id, ReviewRef, changesRequired, reviewerId, TenantId));
    }
}

public sealed record UserAccessReviewCompleted(
    Guid ReviewId, string ReviewRef, bool ChangesRequired, Guid ReviewedBy, Guid TenantId) : DomainEvent;
