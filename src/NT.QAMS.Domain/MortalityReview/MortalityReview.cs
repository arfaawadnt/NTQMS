using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.MortalityReview;

/// <summary>
/// The peer-review classification of a death. Anything other than <see cref="Expected"/> mandates a
/// second (independent) review before the case can be closed.
/// </summary>
public enum DeathClassification { Expected, Unexpected, PotentiallyPreventable, Preventable }

/// <summary>
/// Lifecycle of a mortality review. Expected deaths may close after classification; the rest must
/// pass an independent second review and committee discussion first.
/// </summary>
public enum MortalityStatus { Reported, Classified, SecondReviewed, CommitteeDiscussed, Closed }

/// <summary>
/// A mortality (death) case review (HQMS M10): reported, then peer-reviewed and classified as
/// expected / unexpected / potentially-preventable / preventable. Any non-expected classification
/// mandates an <b>independent</b> second review (the second reviewer must differ from the first —
/// SoD-MRT-001) and committee discussion before closure. Deaths feed the mortality rate against the
/// M24 patient-day denominator.
/// </summary>
public sealed class MortalityReview : AggregateRoot, ITenantScoped, IAllocatable
{
    private MortalityReview()
    {
        ReviewRef = null!;
        PatientRef = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string ReviewRef { get; private set; }
    public string PatientRef { get; private set; }
    public string Unit { get; private set; }
    public DateTimeOffset DeathDateUtc { get; private set; }
    public string? PrimaryDiagnosis { get; private set; }

    public MortalityStatus Status { get; private set; }
    public DeathClassification? Classification { get; private set; }
    public Guid? FirstReviewerId { get; private set; }
    public string? ClassificationFindings { get; private set; }
    public Guid? SecondReviewerId { get; private set; }
    public string? SecondReviewNotes { get; private set; }
    public bool? SecondReviewerConcurs { get; private set; }
    public string? CommitteeLearnings { get; private set; }

    /// <summary>Non-expected deaths require an independent second review before closure.</summary>
    public bool RequiresSecondReview => Classification is not null and not DeathClassification.Expected;

    public static MortalityReview Report(
        string reviewRef, string patientRef, string unit, DateTimeOffset deathDateUtc,
        string? primaryDiagnosis, Guid? departmentId = null)
    {
        if (string.IsNullOrWhiteSpace(patientRef))
        {
            throw new DomainException("MRT-001", "A patient reference is required.");
        }

        if (deathDateUtc == default)
        {
            throw new DomainException("MRT-002", "The date of death is required.");
        }

        return new MortalityReview
        {
            ReviewRef = reviewRef,
            PatientRef = patientRef.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? "Unknown" : unit.Trim(),
            DeathDateUtc = deathDateUtc,
            PrimaryDiagnosis = string.IsNullOrWhiteSpace(primaryDiagnosis) ? null : primaryDiagnosis.Trim(),
            DepartmentId = departmentId,
            Status = MortalityStatus.Reported,
        };
    }

    /// <summary>First peer review: classify the death (Reported ⇒ Classified).</summary>
    public void Classify(Guid reviewerId, DeathClassification classification, string findings)
    {
        if (Status != MortalityStatus.Reported)
        {
            throw new InvalidStateTransitionException("MRT-010", $"Cannot classify a review in state {Status}.");
        }

        if (string.IsNullOrWhiteSpace(findings))
        {
            throw new DomainException("MRT-011", "Classification findings are required.");
        }

        FirstReviewerId = reviewerId;
        Classification = classification;
        ClassificationFindings = findings.Trim();
        Status = MortalityStatus.Classified;
        // M-06: the preventability classification is a committee-grade regulated
        // fact — it reaches the hash-chained ledger via the outbox.
        Raise(new MortalityClassified(Id, ReviewRef, classification.ToString(), reviewerId));
    }

    /// <summary>
    /// Independent second review (Classified ⇒ SecondReviewed). Only for non-expected deaths, and the
    /// second reviewer must not be the first (segregation of duties).
    /// </summary>
    public void RecordSecondReview(Guid reviewerId, string notes, bool concurs)
    {
        if (Status != MortalityStatus.Classified)
        {
            throw new InvalidStateTransitionException("MRT-012", $"Cannot second-review a case in state {Status}.");
        }

        if (!RequiresSecondReview)
        {
            throw new DomainException("MRT-013", "An expected death does not require a second review.");
        }

        if (reviewerId == FirstReviewerId)
        {
            throw new DomainException("MRT-014", "The second review must be performed by a different reviewer (SoD-MRT-001).");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainException("MRT-015", "Second-review notes are required.");
        }

        SecondReviewerId = reviewerId;
        SecondReviewNotes = notes.Trim();
        SecondReviewerConcurs = concurs;
        Status = MortalityStatus.SecondReviewed;
    }

    /// <summary>Records committee discussion and learnings (SecondReviewed ⇒ CommitteeDiscussed).</summary>
    public void MarkCommitteeDiscussed(string learnings)
    {
        if (Status != MortalityStatus.SecondReviewed)
        {
            throw new InvalidStateTransitionException("MRT-016", "Only a second-reviewed case can be marked discussed.");
        }

        if (string.IsNullOrWhiteSpace(learnings))
        {
            throw new DomainException("MRT-017", "Committee learnings are required.");
        }

        CommitteeLearnings = learnings.Trim();
        Status = MortalityStatus.CommitteeDiscussed;
    }

    /// <summary>
    /// Closes the review. An expected death closes straight from Classified; a non-expected death
    /// must have completed committee discussion first.
    /// </summary>
    public void Close()
    {
        var closable = RequiresSecondReview
            ? Status == MortalityStatus.CommitteeDiscussed
            : Status == MortalityStatus.Classified;

        if (!closable)
        {
            throw new InvalidStateTransitionException("MRT-018",
                "The review has not completed the steps required for its classification.");
        }

        Status = MortalityStatus.Closed;
        Raise(new MortalityReviewClosed(Id, ReviewRef));
    }
}

public sealed record MortalityClassified(Guid MortalityReviewId, string ReviewRef, string Classification, Guid ReviewerId) : DomainEvent;

public sealed record MortalityReviewClosed(Guid MortalityReviewId, string ReviewRef) : DomainEvent;
