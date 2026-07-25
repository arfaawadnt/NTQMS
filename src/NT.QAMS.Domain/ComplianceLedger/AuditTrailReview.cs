using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.ComplianceLedger;

public enum AuditTrailReviewStatus { Open, Completed }

/// <summary>
/// Periodic audit-trail review (21 CFR Part 11 data-integrity expectation /
/// WHO-GxP): a named reviewer examines the event trail and field-change ledger
/// for a bounded period and records the conclusion. Completion snapshots the
/// ledger volumes reviewed and, when anomalies are found, raises the event
/// that opens an NC — a suspicious trail is itself a quality incident.
/// Completed reviews are immutable.
/// </summary>
public sealed class AuditTrailReview : AggregateRoot, ITenantScoped
{
    private AuditTrailReview() { ReviewRef = null!; }

    public Guid TenantId { get; set; }
    public string ReviewRef { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public AuditTrailReviewStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    /// <summary>Ledger volumes at completion — evidence of what the reviewer actually covered.</summary>
    public int? EventsReviewed { get; private set; }
    public int? FieldChangesReviewed { get; private set; }
    public bool? AnomaliesFound { get; private set; }
    public string? Conclusion { get; private set; }

    public static AuditTrailReview Open(string reviewRef, DateOnly periodStart, DateOnly periodEnd)
    {
        if (periodEnd < periodStart)
        {
            throw new DomainException("ATR-001", "The review period end must not precede its start.");
        }

        return new AuditTrailReview
        {
            ReviewRef = reviewRef,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = AuditTrailReviewStatus.Open,
        };
    }

    public void Complete(
        Guid reviewerId, DateTimeOffset at, int eventsReviewed, int fieldChangesReviewed,
        bool anomaliesFound, string conclusion)
    {
        if (Status != AuditTrailReviewStatus.Open)
        {
            throw new InvalidStateTransitionException("ATR-010", "The review is already completed and immutable.");
        }

        if (string.IsNullOrWhiteSpace(conclusion))
        {
            throw new DomainException("ATR-011", "A written conclusion is required — 'reviewed' without findings is not evidence.");
        }

        Status = AuditTrailReviewStatus.Completed;
        ReviewedBy = reviewerId;
        CompletedAtUtc = at;
        EventsReviewed = eventsReviewed;
        FieldChangesReviewed = fieldChangesReviewed;
        AnomaliesFound = anomaliesFound;
        Conclusion = conclusion.Trim();

        if (anomaliesFound)
        {
            Raise(new AuditTrailAnomalyFound(Id, ReviewRef, PeriodStart, PeriodEnd, Conclusion, reviewerId, TenantId));
        }
    }
}

public sealed record AuditTrailAnomalyFound(
    Guid ReviewId, string ReviewRef, DateOnly PeriodStart, DateOnly PeriodEnd,
    string Conclusion, Guid ReviewedBy, Guid TenantId) : DomainEvent;
