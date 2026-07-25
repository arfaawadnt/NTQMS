using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum DetectionLimitState { DataEntry, Calculated, SignedOff }

public enum DetectionSampleKind { Blank, LowLevel }

/// <summary>One replicate: a blank, or a low-level sample identified by its assigned concentration.</summary>
public sealed class DetectionMeasurement : Entity
{
    internal DetectionMeasurement(DetectionSampleKind kind, decimal? assignedValue, decimal measuredValue)
    {
        Kind = kind;
        AssignedValue = assignedValue;
        MeasuredValue = measuredValue;
    }

    private DetectionMeasurement() { }

    public DetectionSampleKind Kind { get; private set; }
    /// <summary>The low-level sample's expected concentration (null for blanks).</summary>
    public decimal? AssignedValue { get; private set; }
    public decimal MeasuredValue { get; private set; }
}

/// <summary>Per-low-level summary used for the LoQ (functional-sensitivity) decision — derived, never stored.</summary>
public sealed record LowLevelAssessment(
    decimal AssignedValue, int ReplicateCount, decimal Mean, decimal Sd, decimal CvPct, bool QualifiesForLoq);

/// <summary>
/// Detection-capability study (CLSI EP17, classical parametric approach):
/// blank replicates give LoB = mean_B + 1.645·SD_B; low-level replicates give
/// LoD = LoB + 1.645·SD_L (SD_L pooled within the low-level samples); LoQ is
/// the functional sensitivity — the lowest low-level concentration whose CV
/// meets the study's target AND whose mean lies at or above the LoD. When no
/// level qualifies, LoQ stays honestly unestablished. Measurements are
/// immutable evidence; statistics are derivable-only; sign-off freezes the
/// study.
/// </summary>
public sealed class DetectionLimitStudy : AggregateRoot, ITenantScoped
{
    /// <summary>z for α = β = 0.05 per EP17's classical option.</summary>
    public const decimal Z = 1.645m;

    /// <summary>Floor for a meaningful parametric estimate (EP17 recommends far more across lots/days).</summary>
    public const int MinimumBlankReplicates = 10;
    public const int MinimumLowLevelReplicates = 10;

    private readonly List<DetectionMeasurement> _measurements = [];

    private DetectionLimitStudy()
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
    /// <summary>The CV goal defining functional sensitivity (commonly 20%).</summary>
    public decimal LoqCvTargetPct { get; private set; }
    public DetectionLimitState State { get; private set; }

    // Derived statistics (null until Calculate).
    public decimal? BlankMean { get; private set; }
    public decimal? BlankSd { get; private set; }
    public decimal? PooledLowSd { get; private set; }
    public decimal? Lob { get; private set; }
    public decimal? Lod { get; private set; }
    /// <summary>Null when no low level meets the CV goal at or above the LoD.</summary>
    public decimal? Loq { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<DetectionMeasurement> Measurements => _measurements.AsReadOnly();

    public static DetectionLimitStudy Configure(
        string studyRef, string analyte, string unit, string method, decimal loqCvTargetPct)
    {
        if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(method))
        {
            throw new DomainException("DL-001", "An analyte and the method under study are required.");
        }

        if (loqCvTargetPct is <= 0m or > 50m)
        {
            throw new DomainException("DL-002", "The LoQ CV goal must be a positive percentage (at most 50%).");
        }

        return new DetectionLimitStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            Method = method.Trim(),
            LoqCvTargetPct = loqCvTargetPct,
            State = DetectionLimitState.DataEntry,
        };
    }

    public Guid AddMeasurement(DetectionSampleKind kind, decimal? assignedValue, decimal measuredValue)
    {
        RequireEditable();
        if (kind == DetectionSampleKind.LowLevel && assignedValue is not > 0m)
        {
            throw new DomainException("DL-003", "A low-level replicate needs its sample's positive assigned concentration.");
        }

        if (kind == DetectionSampleKind.Blank && assignedValue is not null)
        {
            throw new DomainException("DL-004", "Blanks carry no assigned concentration.");
        }

        var measurement = new DetectionMeasurement(kind, assignedValue, measuredValue);
        _measurements.Add(measurement);
        Invalidate();
        return measurement.Id;
    }

    public void RemoveMeasurement(Guid measurementId)
    {
        RequireEditable();
        var measurement = _measurements.FirstOrDefault(m => m.Id == measurementId)
            ?? throw new DomainException("DL-404", "Measurement not found.");
        _measurements.Remove(measurement);
        Invalidate();
    }

    public void Calculate()
    {
        RequireEditable();
        var blanks = _measurements.Where(m => m.Kind == DetectionSampleKind.Blank)
            .Select(m => m.MeasuredValue).ToArray();
        var lowLevels = _measurements.Where(m => m.Kind == DetectionSampleKind.LowLevel).ToList();

        if (blanks.Length < MinimumBlankReplicates)
        {
            throw new DomainException("DL-010", $"At least {MinimumBlankReplicates} blank replicates are required.");
        }

        if (lowLevels.Count < MinimumLowLevelReplicates)
        {
            throw new DomainException("DL-011", $"At least {MinimumLowLevelReplicates} low-level replicates are required.");
        }

        var blankMean = blanks.Average();
        var blankSd = SampleSd(blanks, blankMean);
        BlankMean = Math.Round(blankMean, 4);
        BlankSd = Math.Round(blankSd, 4);
        Lob = Math.Round(blankMean + Z * blankSd, 4);

        // SD_L pooled within the low-level samples (each group contributes n−1 df).
        var groups = lowLevels.GroupBy(m => m.AssignedValue!.Value).ToList();
        decimal ssWithin = 0;
        var dfWithin = 0;
        foreach (var g in groups)
        {
            var values = g.Select(m => m.MeasuredValue).ToArray();
            if (values.Length < 2)
            {
                continue; // A single replicate carries no within-sample variance.
            }

            var mean = values.Average();
            ssWithin += values.Sum(v => (v - mean) * (v - mean));
            dfWithin += values.Length - 1;
        }

        if (dfWithin == 0)
        {
            throw new DomainException("DL-012", "Low-level samples need at least two replicates each to pool a within-sample SD.");
        }

        var pooledSd = Sqrt(ssWithin / dfWithin);
        PooledLowSd = Math.Round(pooledSd, 4);
        Lod = Math.Round(Lob.Value + Z * pooledSd, 4);

        // Functional sensitivity: the lowest level meeting the CV goal at or above the LoD.
        Loq = AssessLowLevels(groups)
            .Where(a => a.QualifiesForLoq)
            .OrderBy(a => a.AssignedValue)
            .Select(a => (decimal?)a.AssignedValue)
            .FirstOrDefault();

        State = DetectionLimitState.Calculated;
    }

    /// <summary>Per-low-level table (precision profile) — derived from stored results, never persisted.</summary>
    public IReadOnlyList<LowLevelAssessment> LowLevelAssessments()
    {
        if (Lod is null)
        {
            return [];
        }

        var groups = _measurements.Where(m => m.Kind == DetectionSampleKind.LowLevel)
            .GroupBy(m => m.AssignedValue!.Value).ToList();
        return AssessLowLevels(groups);
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        if (State != DetectionLimitState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "DL-013", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = DetectionLimitState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new DetectionLimitSignedOff(Id, StudyRef, Analyte, Lod!.Value, Loq, TenantId));
    }

    private IReadOnlyList<LowLevelAssessment> AssessLowLevels(
        List<IGrouping<decimal, DetectionMeasurement>> groups)
    {
        var result = new List<LowLevelAssessment>(groups.Count);
        foreach (var g in groups.OrderBy(g => g.Key))
        {
            var values = g.Select(m => m.MeasuredValue).ToArray();
            var mean = values.Average();
            var sd = values.Length > 1 ? SampleSd(values, mean) : 0m;
            var cv = mean == 0m ? 0m : Math.Round(sd / mean * 100m, 2);
            var qualifies = values.Length > 1 && cv <= LoqCvTargetPct && Lod is { } lod && mean >= lod;
            result.Add(new LowLevelAssessment(g.Key, values.Length, Math.Round(mean, 4), Math.Round(sd, 4), cv, qualifies));
        }

        return result;
    }

    private static decimal SampleSd(decimal[] values, decimal mean)
    {
        if (values.Length < 2)
        {
            return 0m;
        }

        var ss = values.Sum(v => (v - mean) * (v - mean));
        return Sqrt(ss / (values.Length - 1));
    }

    private static decimal Sqrt(decimal value) => (decimal)Math.Sqrt((double)value);

    private void Invalidate()
    {
        BlankMean = BlankSd = PooledLowSd = null;
        Lob = Lod = Loq = null;
        if (State == DetectionLimitState.Calculated)
        {
            State = DetectionLimitState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == DetectionLimitState.SignedOff)
        {
            throw new InvalidStateTransitionException("DL-014", "A signed-off study is immutable.");
        }
    }
}

public sealed record DetectionLimitSignedOff(
    Guid StudyId, string StudyRef, string Analyte, decimal Lod, decimal? Loq, Guid TenantId) : DomainEvent;
