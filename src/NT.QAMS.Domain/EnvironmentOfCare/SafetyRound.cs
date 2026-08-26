using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.EnvironmentOfCare;

/// <summary>The environment-of-care programme a safety round covers.</summary>
public enum RoundType { FireSafety, InfectionControl, GeneralSafety, HazardousMaterials, Utilities, Security }

/// <summary>Lifecycle of a safety round.</summary>
public enum RoundStatus { Scheduled, InProgress, Completed }

/// <summary>Severity of a round finding, ordered least to most severe.</summary>
public enum FindingSeverity { Low, Medium, High, Critical }

/// <summary>Whether a finding is still open or has been resolved.</summary>
public enum FindingStatus { Open, Resolved }

/// <summary>A deficiency observed during a safety round, with its severity and resolution.</summary>
public sealed class RoundFinding : Entity
{
    internal RoundFinding(string description, FindingSeverity severity)
    {
        Description = description;
        Severity = severity;
        Status = FindingStatus.Open;
    }

    private RoundFinding() { Description = null!; }

    public string Description { get; private set; }
    public FindingSeverity Severity { get; private set; }
    public FindingStatus Status { get; private set; }
    public string? CorrectiveNote { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    internal void Resolve(string note, DateTimeOffset at)
    {
        Status = FindingStatus.Resolved;
        CorrectiveNote = note;
        ResolvedAtUtc = at;
    }
}

/// <summary>
/// An environment-of-care safety round (HQMS M15): a scheduled inspection of an area against a
/// programme (fire safety, infection control, utilities, …). Findings are logged with a severity
/// during the round and resolved with a corrective note; the round is completed once walked. Open
/// findings — especially High/Critical — are the environmental risk backlog.
/// </summary>
public sealed class SafetyRound : AggregateRoot, ITenantScoped
{
    private readonly List<RoundFinding> _findings = [];

    private SafetyRound()
    {
        RoundRef = null!;
        Area = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string RoundRef { get; private set; }
    public string Area { get; private set; }
    public RoundType Type { get; private set; }
    public DateOnly ScheduledDate { get; private set; }
    public RoundStatus Status { get; private set; }
    public Guid? ConductedBy { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public IReadOnlyList<RoundFinding> Findings => _findings.AsReadOnly();

    public int OpenFindingCount => _findings.Count(f => f.Status == FindingStatus.Open);

    public static SafetyRound Schedule(string roundRef, string area, RoundType type, DateOnly scheduledDate)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            throw new DomainException("EOC-001", "An area is required.");
        }

        return new SafetyRound
        {
            RoundRef = roundRef,
            Area = area.Trim(),
            Type = type,
            ScheduledDate = scheduledDate,
            Status = RoundStatus.Scheduled,
        };
    }

    public void Start(Guid conductedBy)
    {
        if (Status != RoundStatus.Scheduled)
        {
            throw new InvalidStateTransitionException("EOC-010", "Only a scheduled round can be started.");
        }

        ConductedBy = conductedBy;
        Status = RoundStatus.InProgress;
    }

    public Guid AddFinding(string description, FindingSeverity severity)
    {
        if (Status != RoundStatus.InProgress)
        {
            throw new InvalidStateTransitionException("EOC-011", "Findings can only be added while the round is in progress.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("EOC-012", "A finding description is required.");
        }

        var finding = new RoundFinding(description.Trim(), severity);
        _findings.Add(finding);
        return finding.Id;
    }

    public void ResolveFinding(Guid findingId, string note, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException("EOC-013", "A corrective note is required to resolve a finding.");
        }

        var finding = _findings.FirstOrDefault(f => f.Id == findingId)
            ?? throw new DomainException("EOC-014", "Finding not found.");
        if (finding.Status == FindingStatus.Resolved)
        {
            throw new InvalidStateTransitionException("EOC-015", "The finding is already resolved.");
        }

        finding.Resolve(note.Trim(), at);
    }

    public void Complete()
    {
        if (Status != RoundStatus.InProgress)
        {
            throw new InvalidStateTransitionException("EOC-016", "Only a round in progress can be completed.");
        }

        Status = RoundStatus.Completed;
    }
}
