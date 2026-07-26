using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum LinearityState { DataEntry, Calculated, SignedOff }

/// <summary>One replicate measurement of a dilution-series level.</summary>
public sealed class LinearityMeasurement : Entity
{
    internal LinearityMeasurement(decimal assignedValue, decimal measuredValue)
    {
        AssignedValue = assignedValue;
        MeasuredValue = measuredValue;
    }

    private LinearityMeasurement() { }

    /// <summary>The expected/target concentration of the level (levels are grouped by this value).</summary>
    public decimal AssignedValue { get; private set; }
    public decimal MeasuredValue { get; private set; }
}

/// <summary>Per-level assessment derived from the fit — recomputed, never stored.</summary>
public sealed record LinearityLevelAssessment(
    decimal AssignedValue, int ReplicateCount, decimal MeanMeasured,
    decimal FittedValue, decimal DeviationPct, decimal RecoveryPct, bool Passes);

/// <summary>
/// Linearity / AMR verification study (CLSI EP06 2nd-edition style): a
/// dilution series of 5–9 levels measured in replicate. Calculate fits a
/// first-order regression of the level means against the assigned values and
/// assesses each level's percentage deviation from that line against the
/// study's allowable-deviation criterion. The verified analytical measurement
/// range (AMR) is the widest contiguous span of passing levels. Measurements
/// are immutable evidence; statistics are derivable-only; sign-off freezes
/// the study.
/// </summary>
public sealed class LinearityStudy : AggregateRoot, ITenantScoped
{
    /// <summary>EP06 recommends 5–9 levels; below 4 a deviation-from-linearity assessment is not meaningful.</summary>
    public const int MinimumLevels = 4;

    private readonly List<LinearityMeasurement> _measurements = [];

    private LinearityStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
        Method = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    public string Method { get; private set; }
    /// <summary>Allowable per-level deviation from the fitted line, in percent.</summary>
    public decimal AllowableDeviationPct { get; private set; }
    public LinearityState State { get; private set; }

    // Derived statistics (null until Calculate).
    public decimal? Slope { get; private set; }
    public decimal? Intercept { get; private set; }
    public decimal? CorrelationR { get; private set; }
    public bool? IsLinear { get; private set; }
    /// <summary>Verified AMR: the widest contiguous span of passing levels.</summary>
    public decimal? AmrLow { get; private set; }
    public decimal? AmrHigh { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<LinearityMeasurement> Measurements => _measurements.AsReadOnly();

    public static LinearityStudy Configure(
        string studyRef, string analyte, string unit, string method, decimal allowableDeviationPct)
    {
        if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(method))
        {
            throw new DomainException("LIN-001", "An analyte and the method under study are required.");
        }

        if (allowableDeviationPct is <= 0m or > 50m)
        {
            throw new DomainException("LIN-002", "The allowable deviation must be a positive percentage (at most 50%).");
        }

        return new LinearityStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            Method = method.Trim(),
            AllowableDeviationPct = allowableDeviationPct,
            State = LinearityState.DataEntry,
        };
    }

    public Guid AddMeasurement(decimal assignedValue, decimal measuredValue)
    {
        RequireEditable();
        if (assignedValue <= 0m)
        {
            throw new DomainException("LIN-003", "Assigned level concentrations must be positive.");
        }

        var measurement = new LinearityMeasurement(assignedValue, measuredValue);
        _measurements.Add(measurement);
        Invalidate();
        return measurement.Id;
    }

    public void RemoveMeasurement(Guid measurementId)
    {
        RequireEditable();
        var measurement = _measurements.FirstOrDefault(m => m.Id == measurementId)
            ?? throw new DomainException("LIN-404", "Measurement not found.");
        _measurements.Remove(measurement);
        Invalidate();
    }

    /// <summary>
    /// Fits the level means against the assigned values and derives the
    /// linearity verdict and the verified AMR from the per-level deviations.
    /// </summary>
    public void Calculate()
    {
        RequireEditable();
        var levels = GroupLevels();
        if (levels.Count < MinimumLevels)
        {
            throw new DomainException("LIN-010",
                $"At least {MinimumLevels} distinct levels are required (EP06 recommends 5–9).");
        }

        var x = levels.Select(l => (double)l.Assigned).ToArray();
        var y = levels.Select(l => (double)l.Mean).ToArray();
        var n = x.Length;
        var meanX = x.Average();
        var meanY = y.Average();
        double sxx = 0, syy = 0, sxy = 0;
        for (var i = 0; i < n; i++)
        {
            sxx += (x[i] - meanX) * (x[i] - meanX);
            syy += (y[i] - meanY) * (y[i] - meanY);
            sxy += (x[i] - meanX) * (y[i] - meanY);
        }

        if (sxx == 0)
        {
            throw new DomainException("LIN-011", "All levels share one assigned value — a dilution series needs a spread.");
        }

        var slope = sxy / sxx;
        var intercept = meanY - slope * meanX;
        Slope = Round(slope);
        Intercept = Round(intercept);
        CorrelationR = syy == 0 ? 1m : Round(sxy / Math.Sqrt(sxx * syy));

        var assessments = Assess(levels, (decimal)slope, (decimal)intercept, AllowableDeviationPct);
        IsLinear = assessments.All(a => a.Passes);

        // Verified AMR: when the full range fails, a nonlinear extreme also
        // distorts the full-range fit, so sub-ranges are REFITTED on their own
        // levels (EP06 range-restriction practice) — the AMR is the passing
        // contiguous window with the most levels (ties broken by span).
        (AmrLow, AmrHigh) = BestPassingWindow(levels, AllowableDeviationPct);

        State = LinearityState.Calculated;
    }

    /// <summary>Per-level table for reporting — derived from the stored fit, never persisted.</summary>
    public IReadOnlyList<LinearityLevelAssessment> LevelAssessments()
    {
        if (Slope is not { } slope || Intercept is not { } intercept)
        {
            return [];
        }

        return Assess(GroupLevels(), slope, intercept, AllowableDeviationPct);
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001");
        if (State != LinearityState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "LIN-012", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = LinearityState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new LinearityStudySignedOff(Id, StudyRef, Analyte, IsLinear!.Value, TenantId));
    }

    private List<(decimal Assigned, decimal Mean, int Count)> GroupLevels() =>
        _measurements
            .GroupBy(m => m.AssignedValue)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Average(m => m.MeasuredValue), g.Count()))
            .ToList();

    private static IReadOnlyList<LinearityLevelAssessment> Assess(
        List<(decimal Assigned, decimal Mean, int Count)> levels,
        decimal slope, decimal intercept, decimal allowablePct)
    {
        var result = new List<LinearityLevelAssessment>(levels.Count);
        foreach (var (assigned, mean, count) in levels)
        {
            var fitted = slope * assigned + intercept;
            var deviationPct = fitted == 0m ? 0m : Math.Round((mean - fitted) / fitted * 100m, 3);
            var recoveryPct = Math.Round(mean / assigned * 100m, 2);
            result.Add(new LinearityLevelAssessment(
                assigned, count, Math.Round(mean, 4), Math.Round(fitted, 4),
                deviationPct, recoveryPct, Math.Abs(deviationPct) <= allowablePct));
        }

        return result;
    }

    private static (decimal? Low, decimal? High) BestPassingWindow(
        List<(decimal Assigned, decimal Mean, int Count)> levels, decimal allowablePct)
    {
        decimal? bestLow = null, bestHigh = null;
        var bestLen = 0;
        decimal bestSpan = 0;
        for (var start = 0; start < levels.Count; start++)
        {
            for (var end = start + MinimumLevels - 1; end < levels.Count; end++)
            {
                var window = levels[start..(end + 1)];
                if (!WindowPasses(window, allowablePct))
                {
                    continue;
                }

                var len = window.Count;
                var span = window[^1].Assigned - window[0].Assigned;
                if (len > bestLen || (len == bestLen && span > bestSpan))
                {
                    bestLen = len;
                    bestSpan = span;
                    bestLow = window[0].Assigned;
                    bestHigh = window[^1].Assigned;
                }
            }
        }

        return (bestLow, bestHigh);
    }

    /// <summary>Refits the window on its own levels and checks every deviation against the criterion.</summary>
    private static bool WindowPasses(List<(decimal Assigned, decimal Mean, int Count)> window, decimal allowablePct)
    {
        var x = window.Select(l => (double)l.Assigned).ToArray();
        var y = window.Select(l => (double)l.Mean).ToArray();
        var meanX = x.Average();
        var meanY = y.Average();
        double sxx = 0, sxy = 0;
        for (var i = 0; i < x.Length; i++)
        {
            sxx += (x[i] - meanX) * (x[i] - meanX);
            sxy += (x[i] - meanX) * (y[i] - meanY);
        }

        if (sxx == 0)
        {
            return false;
        }

        var slope = (decimal)(sxy / sxx);
        var intercept = (decimal)meanY - slope * (decimal)meanX;
        return Assess(window, slope, intercept, allowablePct).All(a => a.Passes);
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private void Invalidate()
    {
        Slope = Intercept = CorrelationR = null;
        IsLinear = null;
        AmrLow = AmrHigh = null;
        if (State == LinearityState.Calculated)
        {
            State = LinearityState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == LinearityState.SignedOff)
        {
            throw new InvalidStateTransitionException("LIN-013", "A signed-off study is immutable.");
        }
    }
}

public sealed record LinearityStudySignedOff(
    Guid StudyId, string StudyRef, string Analyte, bool IsLinear, Guid TenantId) : DomainEvent;
