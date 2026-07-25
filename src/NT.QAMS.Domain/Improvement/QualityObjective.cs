using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Improvement;

/// <summary>Which way the metric must move to count as achieved.</summary>
public enum ObjectiveDirection { AtLeast, AtMost }

public enum ObjectiveStatus { Active, Achieved, Missed, Cancelled }

/// <summary>One dated measurement of the objective's metric — append-only.</summary>
public sealed class ObjectiveProgressUpdate : Entity
{
    internal ObjectiveProgressUpdate(DateOnly measuredOn, decimal value, Guid recordedById, string? comment)
    {
        MeasuredOn = measuredOn;
        Value = value;
        RecordedById = recordedById;
        Comment = comment;
    }

    private ObjectiveProgressUpdate() { }

    public DateOnly MeasuredOn { get; private set; }
    public decimal Value { get; private set; }
    public Guid RecordedById { get; private set; }
    public string? Comment { get; private set; }
}

/// <summary>
/// Measurable quality objective (ISO 9001 §6.2 / ISO 17025 §8.2): a metric, a
/// numeric target with a direction, an owner and a period. Progress is a
/// series of dated measurements; closure is honest — an objective can only be
/// closed as Achieved when its latest measurement actually meets the target.
/// Cancellation requires a reason. Closed objectives are immutable.
/// </summary>
public sealed class QualityObjective : AggregateRoot, ITenantScoped, IAllocatable
{
    private readonly List<ObjectiveProgressUpdate> _updates = [];

    private QualityObjective()
    {
        ObjectiveRef = null!;
        Title = null!;
        Metric = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string ObjectiveRef { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    /// <summary>What is measured, e.g. "% of NCs closed within 30 days".</summary>
    public string Metric { get; private set; }
    public string Unit { get; private set; }
    public decimal TargetValue { get; private set; }
    public ObjectiveDirection Direction { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public ObjectiveStatus Status { get; private set; }
    public string? ClosureNote { get; private set; }

    public IReadOnlyList<ObjectiveProgressUpdate> Updates => _updates.AsReadOnly();

    public static QualityObjective Define(
        string objectiveRef, string title, string? description, string metric, string unit,
        decimal targetValue, ObjectiveDirection direction, Guid ownerId,
        DateOnly periodStart, DateOnly periodEnd)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(metric))
        {
            throw new DomainException("OBJ-001", "A title and a measurable metric are required.");
        }

        if (periodEnd <= periodStart)
        {
            throw new DomainException("OBJ-002", "The objective period end must fall after its start.");
        }

        return new QualityObjective
        {
            ObjectiveRef = objectiveRef,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Metric = metric.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            TargetValue = targetValue,
            Direction = direction,
            OwnerId = ownerId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = ObjectiveStatus.Active,
        };
    }

    /// <summary>Latest measured value, or null before the first measurement.</summary>
    public decimal? CurrentValue =>
        _updates.OrderByDescending(u => u.MeasuredOn).Select(u => (decimal?)u.Value).FirstOrDefault();

    /// <summary>Whether the latest measurement meets the target (null before any measurement).</summary>
    public bool? OnTarget => CurrentValue is not { } current
        ? null
        : Direction == ObjectiveDirection.AtLeast ? current >= TargetValue : current <= TargetValue;

    public Guid RecordProgress(DateOnly measuredOn, decimal value, Guid recordedById, string? comment)
    {
        if (Status != ObjectiveStatus.Active)
        {
            throw new InvalidStateTransitionException("OBJ-010", $"Progress can only be recorded on an active objective (current: {Status}).");
        }

        var update = new ObjectiveProgressUpdate(
            measuredOn, value, recordedById,
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim());
        _updates.Add(update);
        return update.Id;
    }

    public void CloseAsAchieved(string closureNote)
    {
        RequireActive();
        if (OnTarget != true)
        {
            throw new DomainException("OBJ-011",
                "The latest measurement does not meet the target — an objective cannot be declared achieved against the evidence.");
        }

        Close(ObjectiveStatus.Achieved, closureNote);
    }

    public void CloseAsMissed(string closureNote) => CloseWithRequiredNote(ObjectiveStatus.Missed, closureNote);

    public void Cancel(string reason) => CloseWithRequiredNote(ObjectiveStatus.Cancelled, reason);

    private void CloseWithRequiredNote(ObjectiveStatus status, string note)
    {
        RequireActive();
        Close(status, note);
    }

    private void Close(ObjectiveStatus status, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException("OBJ-012", "A closure note is required.");
        }

        Status = status;
        ClosureNote = note.Trim();
    }

    private void RequireActive()
    {
        if (Status != ObjectiveStatus.Active)
        {
            throw new InvalidStateTransitionException("OBJ-013", $"The objective is already {Status} and immutable.");
        }
    }
}
