using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum InterferenceState { DataEntry, Calculated, SignedOff }

/// <summary>
/// A replicate: a control (unspiked) reading, or a test reading spiked with a
/// named interferent. Control rows carry no interferent label.
/// </summary>
public sealed class InterferenceMeasurement : Entity
{
    internal InterferenceMeasurement(bool isControl, string? interferent, decimal value)
    {
        IsControl = isControl;
        Interferent = interferent;
        Value = value;
    }

    private InterferenceMeasurement() { }

    public bool IsControl { get; private set; }
    /// <summary>Interferent name (null for control rows).</summary>
    public string? Interferent { get; private set; }
    public decimal Value { get; private set; }
}

/// <summary>Per-interferent result: the observed bias against the shared control.</summary>
public sealed record InterferenceResult(
    string Interferent, int ReplicateCount, decimal MeanTest, decimal BiasPct, bool SignificantInterference);

/// <summary>
/// Interference / analytical-specificity study (CLSI EP07): a control pool is
/// measured, then the same pool spiked with each candidate interferent. The
/// percentage bias of each spiked set against the control is assessed against
/// the allowable-bias criterion; a bias beyond it is flagged as significant
/// interference. Measurements are immutable; results are derivable-only;
/// sign-off freezes the study.
/// </summary>
public sealed class InterferenceStudy : AggregateRoot, ITenantScoped
{
    public const int MinimumControlReplicates = 3;

    private readonly List<InterferenceMeasurement> _measurements = [];

    private InterferenceStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    /// <summary>Maximum bias attributable to an interferent before it is significant, as a percentage.</summary>
    public decimal AllowableBiasPct { get; private set; }
    public InterferenceState State { get; private set; }

    // Derived (null until Calculate).
    public decimal? ControlMean { get; private set; }
    public int? InterferentCount { get; private set; }
    public int? SignificantCount { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<InterferenceMeasurement> Measurements => _measurements.AsReadOnly();

    public static InterferenceStudy Configure(
        string studyRef, string analyte, string unit, decimal allowableBiasPct)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("INT-001", "An analyte is required.");
        }

        if (allowableBiasPct is <= 0m or > 100m)
        {
            throw new DomainException("INT-002", "The allowable bias must be a positive percentage.");
        }

        return new InterferenceStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            AllowableBiasPct = allowableBiasPct,
            State = InterferenceState.DataEntry,
        };
    }

    public Guid AddControl(decimal value)
    {
        RequireEditable();
        var m = new InterferenceMeasurement(isControl: true, interferent: null, value);
        _measurements.Add(m);
        Invalidate();
        return m.Id;
    }

    public Guid AddTest(string interferent, decimal value)
    {
        RequireEditable();
        if (string.IsNullOrWhiteSpace(interferent))
        {
            throw new DomainException("INT-003", "A test reading needs the interferent name.");
        }

        var m = new InterferenceMeasurement(isControl: false, interferent.Trim(), value);
        _measurements.Add(m);
        Invalidate();
        return m.Id;
    }

    public void RemoveMeasurement(Guid measurementId)
    {
        RequireEditable();
        var m = _measurements.FirstOrDefault(x => x.Id == measurementId)
            ?? throw new DomainException("INT-404", "Measurement not found.");
        _measurements.Remove(m);
        Invalidate();
    }

    public void Calculate()
    {
        RequireEditable();
        var controls = _measurements.Where(m => m.IsControl).Select(m => m.Value).ToList();
        var tests = _measurements.Where(m => !m.IsControl).ToList();

        if (controls.Count < MinimumControlReplicates)
        {
            throw new DomainException("INT-010", $"At least {MinimumControlReplicates} control replicates are required.");
        }

        if (tests.Count == 0)
        {
            throw new DomainException("INT-011", "At least one interferent test set is required.");
        }

        var controlMean = controls.Average();
        if (controlMean == 0m)
        {
            throw new DomainException("INT-012", "The control mean is zero — a percentage bias cannot be computed.");
        }

        ControlMean = Math.Round(controlMean, 4);
        var groups = tests.GroupBy(m => m.Interferent!).ToList();
        InterferentCount = groups.Count;
        SignificantCount = Assess((double)controlMean).Count(r => r.SignificantInterference);
        State = InterferenceState.Calculated;
    }

    /// <summary>Per-interferent bias table — derived from the stored control mean, never persisted.</summary>
    public IReadOnlyList<InterferenceResult> Results()
    {
        if (ControlMean is not { } controlMean)
        {
            return [];
        }

        return Assess((double)controlMean);
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        if (State != InterferenceState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "INT-013", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = InterferenceState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new InterferenceStudySignedOff(Id, StudyRef, Analyte, SignificantCount!.Value, TenantId));
    }

    private IReadOnlyList<InterferenceResult> Assess(double controlMean)
    {
        return _measurements
            .Where(m => !m.IsControl)
            .GroupBy(m => m.Interferent!)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var meanTest = g.Average(m => (double)m.Value);
                var biasPct = (meanTest - controlMean) / controlMean * 100.0;
                var rounded = Math.Round((decimal)biasPct, 3);
                return new InterferenceResult(
                    g.Key, g.Count(), Math.Round((decimal)meanTest, 4), rounded,
                    Math.Abs(rounded) > AllowableBiasPct);
            })
            .ToList();
    }

    private void Invalidate()
    {
        ControlMean = null;
        InterferentCount = SignificantCount = null;
        if (State == InterferenceState.Calculated)
        {
            State = InterferenceState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == InterferenceState.SignedOff)
        {
            throw new InvalidStateTransitionException("INT-014", "A signed-off study is immutable.");
        }
    }
}

public sealed record InterferenceStudySignedOff(
    Guid StudyId, string StudyRef, string Analyte, int SignificantCount, Guid TenantId) : DomainEvent;
