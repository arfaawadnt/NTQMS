using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Competency;

public enum CompetencyStatus { PendingTraining, Evaluated, Authorized, Revoked }

public sealed class AssessmentResult : Entity
{
    internal AssessmentResult(int score, Guid assessorId, DateTimeOffset assessedAt)
    {
        Score = score;
        AssessorId = assessorId;
        AssessedAtUtc = assessedAt;
    }

    private AssessmentResult() { }

    public int Score { get; private set; }
    public Guid AssessorId { get; private set; }
    public DateTimeOffset AssessedAtUtc { get; private set; }
}

/// <summary>
/// Competency per (trainee, subject) — FR-GOV-02/FR-TRAIN-SCORE: authorization
/// requires a score of at least 80 and an assessor who is not the trainee
/// (SoD rule 4, SOD-COMP-001). Expiry returns the record to PendingTraining
/// (requalification); revocation is terminal. Assessment attempts are
/// append-only children.
/// </summary>
public sealed class CompetencyRecord : AggregateRoot, ITenantScoped
{
    public const int PassMark = 80;

    private readonly List<AssessmentResult> _assessments = [];

    private CompetencyRecord()
    {
        Subject = null!;
    }

    public Guid TenantId { get; set; }
    public Guid TraineeId { get; private set; }
    public string Subject { get; private set; }
    public Guid? DocumentId { get; private set; }
    public CompetencyStatus Status { get; private set; }
    public int ValidityMonths { get; private set; }
    public DateOnly? ExpiresAt { get; private set; }
    public Guid? AuthorizedBy { get; private set; }
    public string? RevocationReason { get; private set; }

    public IReadOnlyList<AssessmentResult> Assessments => _assessments.AsReadOnly();

    public static CompetencyRecord Assign(
        Guid traineeId, string subject, Guid? documentId, int validityMonths)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new DomainException("COMP-001", "A competency subject is required (e.g. an SOP or method).");
        }

        if (validityMonths < 1)
        {
            throw new DomainException("COMP-002", "Validity must be at least one month.");
        }

        return new CompetencyRecord
        {
            TraineeId = traineeId,
            Subject = subject.Trim(),
            DocumentId = documentId,
            Status = CompetencyStatus.PendingTraining,
            ValidityMonths = validityMonths,
        };
    }

    public void ScoreAssessment(int score, Guid assessorId, DateTimeOffset at)
    {
        if (Status is not (CompetencyStatus.PendingTraining or CompetencyStatus.Evaluated))
        {
            throw new InvalidStateTransitionException("COMP-010", $"Cannot score an assessment in state {Status}.");
        }

        if (score is < 0 or > 100)
        {
            throw new DomainException("COMP-011", "Score must be between 0 and 100.");
        }

        if (assessorId == TraineeId)
        {
            throw new DomainException("SOD-COMP-001", "Segregation of duties: a trainee cannot assess their own competency.");
        }

        _assessments.Add(new AssessmentResult(score, assessorId, at));
        Status = score >= PassMark ? CompetencyStatus.Evaluated : CompetencyStatus.PendingTraining;
    }

    public void Authorize(Guid actorId, DateOnly asOf)
    {
        if (Status != CompetencyStatus.Evaluated)
        {
            throw new InvalidStateTransitionException(
                "COMP-012", $"Only an Evaluated competency (score >= {PassMark}) can be authorized.");
        }

        if (actorId == TraineeId)
        {
            throw new DomainException("SOD-COMP-001", "Segregation of duties: a trainee cannot authorize their own competency.");
        }

        Status = CompetencyStatus.Authorized;
        AuthorizedBy = actorId;
        ExpiresAt = asOf.AddMonths(ValidityMonths);
        Raise(new CompetencyAuthorized(Id, TraineeId, Subject, ExpiresAt.Value, TenantId));
    }

    /// <summary>Sweep-proposed: Authorized + past expiry → back to PendingTraining (requalify).</summary>
    public void ExpireIfDue(DateOnly asOf)
    {
        if (Status != CompetencyStatus.Authorized || ExpiresAt is null || ExpiresAt > asOf)
        {
            return;
        }

        Status = CompetencyStatus.PendingTraining;
        AuthorizedBy = null;
        Raise(new CompetencyExpired(Id, TraineeId, Subject, TenantId));
    }

    public void Revoke(Guid actorId, string reason)
    {
        if (Status != CompetencyStatus.Authorized)
        {
            throw new InvalidStateTransitionException("COMP-013", "Only an Authorized competency can be revoked.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("COMP-014", "A revocation reason is required.");
        }

        Status = CompetencyStatus.Revoked;
        RevocationReason = reason.Trim();
        Raise(new CompetencyRevoked(Id, TraineeId, Subject, actorId, RevocationReason, TenantId));
    }
}

/// <summary>Manual (or future policy-driven) training work item; assessment happens on the CompetencyRecord.</summary>
public sealed class TrainingAssignment : AggregateRoot, ITenantScoped
{
    private TrainingAssignment()
    {
        Subject = null!;
    }

    public Guid TenantId { get; set; }
    public Guid TraineeId { get; private set; }
    public string Subject { get; private set; }
    public Guid? DocumentId { get; private set; }
    public DateOnly DueDate { get; private set; }
    public bool Completed { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static TrainingAssignment Create(Guid traineeId, string subject, Guid? documentId, DateOnly dueDate)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new DomainException("TRN-001", "A training subject is required.");
        }

        return new TrainingAssignment
        {
            TraineeId = traineeId,
            Subject = subject.Trim(),
            DocumentId = documentId,
            DueDate = dueDate,
        };
    }

    public void Complete(DateTimeOffset at)
    {
        if (Completed)
        {
            throw new InvalidStateTransitionException("TRN-002", "Training is already completed.");
        }

        Completed = true;
        CompletedAtUtc = at;
    }
}

public sealed record CompetencyAuthorized(
    Guid CompetencyId, Guid TraineeId, string Subject, DateOnly ExpiresAt, Guid TenantId) : DomainEvent;

public sealed record CompetencyExpired(
    Guid CompetencyId, Guid TraineeId, string Subject, Guid TenantId) : DomainEvent;

public sealed record CompetencyRevoked(
    Guid CompetencyId, Guid TraineeId, string Subject, Guid RevokedBy, string Reason, Guid TenantId) : DomainEvent;
