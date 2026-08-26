using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.TrainingManagement;

/// <summary>Lifecycle of a scheduled delivery of a course.</summary>
public enum SessionStatus { Scheduled, Held, Closed, Cancelled }

/// <summary>
/// One trainee's participation in a session, carrying the pre- and post-assessment scores that
/// measure training effectiveness (the score gain) and whether the post-assessment was passed.
/// </summary>
public sealed class SessionAttendance : Entity
{
    internal SessionAttendance(Guid traineeId)
    {
        TraineeId = traineeId;
    }

    private SessionAttendance() { }

    public Guid TraineeId { get; private set; }
    public bool Attended { get; private set; }
    public int? PreScore { get; private set; }
    public int? PostScore { get; private set; }
    public bool Passed { get; private set; }

    /// <summary>The pre/post score gain, when both were captured — the individual effectiveness figure.</summary>
    public int? ScoreGain => PreScore.HasValue && PostScore.HasValue ? PostScore - PreScore : null;

    internal void Record(bool attended, int? preScore, int? postScore, int passMark)
    {
        Attended = attended;
        PreScore = preScore;
        PostScore = postScore;
        // Passing requires attendance AND a post score at or above the course pass mark.
        Passed = attended && postScore.HasValue && postScore.Value >= passMark;
    }
}

/// <summary>
/// A scheduled delivery of a <see cref="TrainingCourse"/> (HQMS M12): trainees are registered,
/// the session is held, attendance and pre/post assessment scores are recorded, then it is closed.
/// The session references its course by id (a separate aggregate) and is passed the course pass
/// mark when recording attendance. Scheduled → Held → Closed (or Cancelled before it is held).
/// </summary>
public sealed class TrainingSession : AggregateRoot, ITenantScoped
{
    private readonly List<SessionAttendance> _attendance = [];

    private TrainingSession()
    {
        SessionRef = null!;
        Location = null!;
        TrainerName = null!;
    }

    public Guid TenantId { get; set; }
    public Guid CourseId { get; private set; }
    public string SessionRef { get; private set; }
    public DateTimeOffset ScheduledAtUtc { get; private set; }
    public string Location { get; private set; }
    public string TrainerName { get; private set; }
    public SessionStatus Status { get; private set; }

    public IReadOnlyList<SessionAttendance> Attendance => _attendance.AsReadOnly();

    /// <summary>Trainees recorded as having attended.</summary>
    public int AttendedCount => _attendance.Count(a => a.Attended);

    public static TrainingSession Schedule(
        Guid courseId, string sessionRef, DateTimeOffset scheduledAtUtc, string location, string trainerName)
    {
        if (courseId == Guid.Empty)
        {
            throw new DomainException("SES-001", "A course is required.");
        }

        if (scheduledAtUtc == default)
        {
            throw new DomainException("SES-002", "A scheduled date is required.");
        }

        return new TrainingSession
        {
            CourseId = courseId,
            SessionRef = sessionRef,
            ScheduledAtUtc = scheduledAtUtc,
            Location = string.IsNullOrWhiteSpace(location) ? "Unspecified" : location.Trim(),
            TrainerName = string.IsNullOrWhiteSpace(trainerName) ? "Unspecified" : trainerName.Trim(),
            Status = SessionStatus.Scheduled,
        };
    }

    public Guid RegisterAttendee(Guid traineeId)
    {
        if (Status is not (SessionStatus.Scheduled or SessionStatus.Held))
        {
            throw new InvalidStateTransitionException("SES-010", $"Cannot register a trainee for a {Status} session.");
        }

        if (_attendance.Any(a => a.TraineeId == traineeId))
        {
            throw new DomainException("SES-011", "The trainee is already registered for this session.");
        }

        var line = new SessionAttendance(traineeId);
        _attendance.Add(line);
        return line.Id;
    }

    public void Hold()
    {
        if (Status != SessionStatus.Scheduled)
        {
            throw new InvalidStateTransitionException("SES-012", "Only a scheduled session can be held.");
        }

        Status = SessionStatus.Held;
    }

    /// <summary>
    /// Records a registered trainee's attendance and pre/post scores. The pass mark is supplied by the
    /// course aggregate (SES cannot read another aggregate's state directly).
    /// </summary>
    public void RecordAttendance(Guid traineeId, bool attended, int? preScore, int? postScore, int passMark)
    {
        if (Status != SessionStatus.Held)
        {
            throw new InvalidStateTransitionException("SES-013", "Attendance can only be recorded for a held session.");
        }

        if (preScore is < 0 or > 100 || postScore is < 0 or > 100)
        {
            throw new DomainException("SES-014", "Scores must be between 0 and 100.");
        }

        var line = _attendance.FirstOrDefault(a => a.TraineeId == traineeId)
            ?? throw new DomainException("SES-015", "The trainee is not registered for this session.");
        line.Record(attended, preScore, postScore, passMark);
    }

    public void Close()
    {
        if (Status != SessionStatus.Held)
        {
            throw new InvalidStateTransitionException("SES-016", "Only a held session can be closed.");
        }

        Status = SessionStatus.Closed;
    }

    public void Cancel()
    {
        if (Status is not (SessionStatus.Scheduled or SessionStatus.Held))
        {
            throw new InvalidStateTransitionException("SES-017", "Only a scheduled or held session can be cancelled.");
        }

        Status = SessionStatus.Cancelled;
    }
}
