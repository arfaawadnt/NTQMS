using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.RiskGovernance;

/// <summary>Ordinary process FMEA, or the healthcare variant applied to clinical processes.</summary>
public enum FmeaType { Fmea, Hfmea }

/// <summary>Lifecycle of an FMEA worksheet.</summary>
public enum FmeaStatus { Draft, Active, Closed }

/// <summary>Whether a failure mode still needs action or has been re-scored after action.</summary>
public enum FailureModeStatus { Open, Actioned }

/// <summary>
/// One line of the FMEA worksheet: a way a process step can fail, its effect and cause,
/// and the Severity / Occurrence / Detection ratings whose product is the Risk Priority
/// Number. A recommended action and a post-action residual re-score capture the improvement.
/// Ratings are on the standard 1–10 scale.
/// </summary>
public sealed class FailureMode : Entity
{
    internal FailureMode(
        string processStep, string failureMode, string effect, string cause,
        int severity, int occurrence, int detection)
    {
        ProcessStep = processStep;
        FailureModeText = failureMode;
        Effect = effect;
        Cause = cause;
        Severity = severity;
        Occurrence = occurrence;
        Detection = detection;
        Rpn = severity * occurrence * detection;
        Status = FailureModeStatus.Open;
    }

    private FailureMode()
    {
        ProcessStep = null!;
        FailureModeText = null!;
        Effect = null!;
        Cause = null!;
    }

    public string ProcessStep { get; private set; }
    public string FailureModeText { get; private set; }
    public string Effect { get; private set; }
    public string Cause { get; private set; }
    public int Severity { get; private set; }
    public int Occurrence { get; private set; }
    public int Detection { get; private set; }

    /// <summary>Risk Priority Number — Severity × Occurrence × Detection (1–1000).</summary>
    public int Rpn { get; private set; }

    public string? RecommendedAction { get; private set; }
    public Guid? ActionOwnerId { get; private set; }
    public int? ResidualSeverity { get; private set; }
    public int? ResidualOccurrence { get; private set; }
    public int? ResidualDetection { get; private set; }

    /// <summary>Risk Priority Number after the recommended action, once re-scored.</summary>
    public int? ResidualRpn { get; private set; }

    public FailureModeStatus Status { get; private set; }

    internal void Recommend(string action, Guid? ownerId)
    {
        RecommendedAction = action;
        ActionOwnerId = ownerId;
    }

    internal void RecordResidual(int severity, int occurrence, int detection)
    {
        // M-22: a failure mode may only become Actioned once a recommended action
        // is on record — otherwise "Actioned" is a false prospective-risk claim.
        if (string.IsNullOrWhiteSpace(RecommendedAction))
        {
            throw new DomainException(
                "FME-020", "A recommended action must be recorded before the residual risk is scored.");
        }

        ResidualSeverity = severity;
        ResidualOccurrence = occurrence;
        ResidualDetection = detection;
        ResidualRpn = severity * occurrence * detection;
        Status = FailureModeStatus.Actioned;
    }
}

/// <summary>
/// An FMEA / HFMEA worksheet (HQMS M04): prospective analysis of how a process can fail
/// before harm occurs. Holds its failure modes, each scored to an RPN so the worksheet can
/// be worked highest-risk first. Draft while it is being built, Active while in use, Closed
/// when the analysis cycle ends. Complements the reactive risk register.
/// </summary>
public sealed class FmeaStudy : AggregateRoot, ITenantScoped, IAllocatable
{
    /// <summary>The RPN at or above which a failure mode is treated as a priority (standard FMEA convention).</summary>
    public const int HighRpnThreshold = 100;

    private readonly List<FailureMode> _failureModes = [];

    private FmeaStudy()
    {
        FmeaRef = null!;
        Title = null!;
        ProcessName = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string FmeaRef { get; private set; }
    public string Title { get; private set; }
    public string ProcessName { get; private set; }
    public FmeaType Type { get; private set; }
    public FmeaStatus Status { get; private set; }

    public IReadOnlyList<FailureMode> FailureModes => _failureModes.AsReadOnly();

    public static FmeaStudy Create(string fmeaRef, string title, string processName, FmeaType type)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("FME-001", "An FMEA title is required.");
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new DomainException("FME-002", "The analysed process is required.");
        }

        return new FmeaStudy
        {
            FmeaRef = fmeaRef,
            Title = title.Trim(),
            ProcessName = processName.Trim(),
            Type = type,
            Status = FmeaStatus.Draft,
        };
    }

    public Guid AddFailureMode(
        string processStep, string failureMode, string effect, string cause,
        int severity, int occurrence, int detection)
    {
        RequireOpen("FME-010", "add a failure mode to");
        if (string.IsNullOrWhiteSpace(processStep) || string.IsNullOrWhiteSpace(failureMode))
        {
            throw new DomainException("FME-011", "A process step and failure mode are required.");
        }

        ValidateRatings(severity, occurrence, detection);

        var mode = new FailureMode(
            processStep.Trim(), failureMode.Trim(), (effect ?? string.Empty).Trim(), (cause ?? string.Empty).Trim(),
            severity, occurrence, detection);
        _failureModes.Add(mode);
        return mode.Id;
    }

    public void RecommendAction(Guid failureModeId, string action, Guid? ownerId)
    {
        RequireOpen("FME-012", "recommend an action on");
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException("FME-013", "A recommended action is required.");
        }

        Mode(failureModeId).Recommend(action.Trim(), ownerId);
    }

    public void RecordResidual(Guid failureModeId, int severity, int occurrence, int detection)
    {
        RequireOpen("FME-014", "re-score");
        ValidateRatings(severity, occurrence, detection);
        Mode(failureModeId).RecordResidual(severity, occurrence, detection);
    }

    public void Activate()
    {
        if (Status != FmeaStatus.Draft)
        {
            throw new InvalidStateTransitionException("FME-015", $"Cannot activate an FMEA in state {Status}.");
        }

        if (_failureModes.Count == 0)
        {
            throw new DomainException("FME-016", "An FMEA needs at least one failure mode before activation.");
        }

        Status = FmeaStatus.Active;
        Raise(new FmeaActivated(Id, FmeaRef, ProcessName, _failureModes.Count));
    }

    public void Close()
    {
        if (Status != FmeaStatus.Active)
        {
            throw new InvalidStateTransitionException("FME-017", $"Cannot close an FMEA in state {Status}.");
        }

        Status = FmeaStatus.Closed;
        Raise(new FmeaClosed(Id, FmeaRef));
    }

    private FailureMode Mode(Guid id) =>
        _failureModes.FirstOrDefault(m => m.Id == id)
        ?? throw new DomainException("FME-018", "Failure mode not found in this FMEA.");

    private void RequireOpen(string code, string action)
    {
        if (Status == FmeaStatus.Closed)
        {
            throw new InvalidStateTransitionException(code, $"Cannot {action} a closed FMEA.");
        }
    }

    private static void ValidateRatings(int severity, int occurrence, int detection)
    {
        if (severity is < 1 or > 10 || occurrence is < 1 or > 10 || detection is < 1 or > 10)
        {
            throw new DomainException(
                "FME-019", "Severity, occurrence and detection must each be explicitly rated 1–10.");
        }
    }
}

public sealed record FmeaActivated(Guid FmeaId, string FmeaRef, string ProcessName, int FailureModeCount) : DomainEvent;
public sealed record FmeaClosed(Guid FmeaId, string FmeaRef) : DomainEvent;
