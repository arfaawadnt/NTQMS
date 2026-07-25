using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum OutlierScreeningState { DataEntry, Calculated, SignedOff }

/// <summary>One data point in the screened set.</summary>
public sealed class OutlierDataPoint : Entity
{
    internal OutlierDataPoint(decimal value, string? label)
    {
        Value = value;
        Label = label;
    }

    private OutlierDataPoint() { }

    public decimal Value { get; private set; }
    public string? Label { get; private set; }
}

/// <summary>Per-point verdict — standardised (z) and robust (modified-z) scores, plus the outlier flag.</summary>
public sealed record OutlierPointResult(
    Guid Id, decimal Value, string? Label, decimal ZScore, decimal ModifiedZScore, bool IsOutlier);

/// <summary>
/// Automated outlier detection &amp; data normalisation. Two distribution-free
/// rules run together on a data set: Tukey fences (Q1 − 1.5·IQR, Q3 + 1.5·IQR)
/// and the robust Iglewicz–Hoaglin modified z-score (0.6745·(x − median)/MAD,
/// flagged beyond ±3.5). Each point also carries its standard z-score for
/// normalisation. A point is flagged when either rule trips. Data is immutable
/// evidence; results are derivable-only; sign-off freezes the screening.
/// </summary>
public sealed class OutlierScreening : AggregateRoot, ITenantScoped
{
    public const int MinimumPoints = 4;
    private const decimal ModifiedZThreshold = 3.5m;

    private readonly List<OutlierDataPoint> _points = [];

    private OutlierScreening()
    {
        ScreeningRef = null!;
        Dataset = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public string ScreeningRef { get; private set; }
    /// <summary>What the data set represents, e.g. "Calibrator 3 replicates".</summary>
    public string Dataset { get; private set; }
    public string Unit { get; private set; }
    public OutlierScreeningState State { get; private set; }

    // Derived (null until Calculate).
    public int? PointCount { get; private set; }
    public decimal? Mean { get; private set; }
    public decimal? Sd { get; private set; }
    public decimal? Median { get; private set; }
    public decimal? Q1 { get; private set; }
    public decimal? Q3 { get; private set; }
    public decimal? TukeyLower { get; private set; }
    public decimal? TukeyUpper { get; private set; }
    public int? OutlierCount { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<OutlierDataPoint> Points => _points.AsReadOnly();

    public static OutlierScreening Configure(string screeningRef, string dataset, string unit)
    {
        if (string.IsNullOrWhiteSpace(dataset))
        {
            throw new DomainException("OUT-001", "A data-set description is required.");
        }

        return new OutlierScreening
        {
            ScreeningRef = screeningRef,
            Dataset = dataset.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            State = OutlierScreeningState.DataEntry,
        };
    }

    public Guid AddPoint(decimal value, string? label)
    {
        RequireEditable();
        var point = new OutlierDataPoint(value, string.IsNullOrWhiteSpace(label) ? null : label.Trim());
        _points.Add(point);
        Invalidate();
        return point.Id;
    }

    public void RemovePoint(Guid pointId)
    {
        RequireEditable();
        var point = _points.FirstOrDefault(p => p.Id == pointId)
            ?? throw new DomainException("OUT-404", "Data point not found.");
        _points.Remove(point);
        Invalidate();
    }

    public void Calculate()
    {
        RequireEditable();
        if (_points.Count < MinimumPoints)
        {
            throw new DomainException("OUT-010", $"At least {MinimumPoints} data points are required to screen for outliers.");
        }

        var values = _points.Select(p => (double)p.Value).ToArray();
        var n = values.Length;
        var mean = values.Average();
        var sd = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (n - 1));
        var median = MedianOf(values);
        var q1 = Quantile(values, 0.25);
        var q3 = Quantile(values, 0.75);
        var iqr = q3 - q1;
        var mad = MedianOf(values.Select(v => Math.Abs(v - median)).ToArray());

        Mean = Round(mean);
        Sd = Round(sd);
        Median = Round(median);
        Q1 = Round(q1);
        Q3 = Round(q3);
        TukeyLower = Round(q1 - 1.5 * iqr);
        TukeyUpper = Round(q3 + 1.5 * iqr);
        OutlierCount = _points.Count(p => IsOutlier((double)p.Value, mean, sd, median, mad, q1, q3, iqr));
        PointCount = n;
        State = OutlierScreeningState.Calculated;
    }

    /// <summary>Per-point results for the table/plot — derived from the stored data, never persisted.</summary>
    public IReadOnlyList<OutlierPointResult> PointResults()
    {
        if (State == OutlierScreeningState.DataEntry || _points.Count == 0)
        {
            return [];
        }

        var values = _points.Select(p => (double)p.Value).ToArray();
        var n = values.Length;
        var mean = values.Average();
        var sd = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (n - 1));
        var median = MedianOf(values);
        var mad = MedianOf(values.Select(v => Math.Abs(v - median)).ToArray());
        var q1 = Quantile(values, 0.25);
        var q3 = Quantile(values, 0.75);
        var iqr = q3 - q1;

        return _points
            .OrderBy(p => p.Value)
            .Select(p =>
            {
                var v = (double)p.Value;
                var z = sd == 0 ? 0 : (v - mean) / sd;
                var mz = mad == 0 ? 0 : 0.6745 * (v - median) / mad;
                return new OutlierPointResult(
                    p.Id, p.Value, p.Label, Round(z), Round(mz),
                    IsOutlier(v, mean, sd, median, mad, q1, q3, iqr));
            })
            .ToList();
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        if (State != OutlierScreeningState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "OUT-011", $"Only a calculated screening can be signed off (current: {State}).");
        }

        State = OutlierScreeningState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new OutlierScreeningSignedOff(Id, ScreeningRef, OutlierCount!.Value, TenantId));
    }

    private static bool IsOutlier(double v, double mean, double sd, double median, double mad,
        double q1, double q3, double iqr)
    {
        var beyondTukey = v < q1 - 1.5 * iqr || v > q3 + 1.5 * iqr;
        var modifiedZ = mad == 0 ? 0 : Math.Abs(0.6745 * (v - median) / mad);
        return beyondTukey || modifiedZ > (double)ModifiedZThreshold;
    }

    private static double MedianOf(double[] values)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var m = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2.0;
    }

    /// <summary>Linear-interpolation quantile (type 7), matching common spreadsheet PERCENTILE.</summary>
    private static double Quantile(double[] values, double p)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var pos = p * (sorted.Length - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        if (lo == hi)
        {
            return sorted[lo];
        }

        return sorted[lo] + (pos - lo) * (sorted[hi] - sorted[lo]);
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private void Invalidate()
    {
        PointCount = OutlierCount = null;
        Mean = Sd = Median = Q1 = Q3 = TukeyLower = TukeyUpper = null;
        if (State == OutlierScreeningState.Calculated)
        {
            State = OutlierScreeningState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == OutlierScreeningState.SignedOff)
        {
            throw new InvalidStateTransitionException("OUT-012", "A signed-off screening is immutable.");
        }
    }
}

public sealed record OutlierScreeningSignedOff(
    Guid ScreeningId, string ScreeningRef, int OutlierCount, Guid TenantId) : DomainEvent;
