using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.EnvironmentOfCare;

/// <summary>The kind of emergency drill.</summary>
public enum DrillType { Fire, Evacuation, CodeBlue, Disaster, Hazmat, ActiveShooter }

/// <summary>Lifecycle of a drill: scheduled, executed, then evaluated.</summary>
public enum DrillStatus { Scheduled, Executed, Evaluated }

/// <summary>
/// An emergency-preparedness drill (HQMS M15): scheduled, executed (with a participant count), then
/// evaluated with an effectiveness score and improvement notes. The evaluation closes the loop that
/// feeds improvement actions — a fire drill is scheduled, run, scored, and its gaps acted on.
/// </summary>
public sealed class Drill : AggregateRoot, ITenantScoped
{
    private Drill()
    {
        DrillRef = null!;
        Location = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string DrillRef { get; private set; }
    public DrillType Type { get; private set; }
    public string Location { get; private set; }
    public DateOnly ScheduledDate { get; private set; }
    public DrillStatus Status { get; private set; }
    public DateTimeOffset? ExecutedAtUtc { get; private set; }
    public int? ParticipantCount { get; private set; }
    public int? EvaluationScore { get; private set; }
    public string? ImprovementNotes { get; private set; }

    /// <summary>Effectiveness tier from the evaluation score (null until evaluated).</summary>
    public string? Effectiveness => EvaluationScore switch
    {
        null => null,
        >= 85 => "Effective",
        >= 60 => "PartiallyEffective",
        _ => "Ineffective",
    };

    public static Drill Schedule(string drillRef, DrillType type, string location, DateOnly scheduledDate)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new DomainException("DRL-001", "A location is required.");
        }

        return new Drill
        {
            DrillRef = drillRef,
            Type = type,
            Location = location.Trim(),
            ScheduledDate = scheduledDate,
            Status = DrillStatus.Scheduled,
        };
    }

    public void Execute(DateTimeOffset executedAt, int participantCount)
    {
        if (Status != DrillStatus.Scheduled)
        {
            throw new InvalidStateTransitionException("DRL-010", "Only a scheduled drill can be executed.");
        }

        if (participantCount < 0)
        {
            throw new DomainException("DRL-011", "Participant count cannot be negative.");
        }

        ExecutedAtUtc = executedAt;
        ParticipantCount = participantCount;
        Status = DrillStatus.Executed;
    }

    public void Evaluate(int score, string improvementNotes)
    {
        if (Status != DrillStatus.Executed)
        {
            throw new InvalidStateTransitionException("DRL-012", "Only an executed drill can be evaluated.");
        }

        if (score is < 0 or > 100)
        {
            throw new DomainException("DRL-013", "The evaluation score must be between 0 and 100.");
        }

        EvaluationScore = score;
        ImprovementNotes = string.IsNullOrWhiteSpace(improvementNotes) ? null : improvementNotes.Trim();
        Status = DrillStatus.Evaluated;
        // M-06: the drill evaluation is a regulated readiness fact.
        Raise(new DrillEvaluated(Id, DrillRef, score));
    }
}

public sealed record DrillEvaluated(Guid DrillId, string DrillRef, int Score) : DomainEvent;
