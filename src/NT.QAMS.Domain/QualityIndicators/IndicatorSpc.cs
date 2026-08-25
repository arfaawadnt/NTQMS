namespace NT.QAMS.Domain.QualityIndicators;

/// <summary>One plotted point on the control chart with any special-cause rules it triggered.</summary>
public sealed record SpcPoint(int Index, decimal Value, bool SpecialCause, IReadOnlyList<string> Rules);

/// <summary>
/// The result of a statistical-process-control analysis over an indicator's measurement
/// series: the centre line and control limits, plus each point graded for special-cause
/// variation. Empty (with <see cref="HasLimits"/> false) when there are too few points
/// to compute limits.
/// </summary>
public sealed record SpcAnalysis(
    bool HasLimits,
    decimal Mean, decimal StdDev,
    decimal Ucl, decimal Lcl,
    decimal Upper2Sigma, decimal Lower2Sigma,
    decimal Upper1Sigma, decimal Lower1Sigma,
    IReadOnlyList<SpcPoint> Points)
{
    public static readonly SpcAnalysis Insufficient =
        new(false, 0, 0, 0, 0, 0, 0, 0, 0, []);
}

/// <summary>
/// Computes Shewhart control-chart statistics and flags special-cause variation using
/// the standard rules the specification asks for (M06). Distinguishing real signal from
/// ordinary noise is the whole point: a value moving inside the control limits is not a
/// breach to be actioned, whereas a point beyond 3σ, a run, or a trend is.
///
/// Rules implemented (the widely used Nelson/Western-Electric subset):
///   R1 — one point beyond ±3σ (a control-limit violation);
///   R2 — two of three consecutive points beyond ±2σ on the same side;
///   R3 — eight consecutive points on the same side of the centre line (a shift);
///   R4 — six consecutive points steadily increasing or decreasing (a trend).
///
/// Pure function: no I/O, no state — exhaustively testable.
/// </summary>
public static class IndicatorSpc
{
    /// <summary>Minimum points before control limits are meaningful.</summary>
    public const int MinimumPoints = 4;

    public static SpcAnalysis Analyze(IReadOnlyList<decimal> values)
    {
        if (values is null || values.Count < MinimumPoints)
        {
            return SpcAnalysis.Insufficient;
        }

        var n = values.Count;
        var mean = values.Average();

        // Sample standard deviation (n-1). Computed in double for the sqrt, then carried as decimal.
        var variance = values.Sum(v => (double)((v - mean) * (v - mean))) / (n - 1);
        var sd = (decimal)Math.Sqrt(variance);

        var ucl = mean + (3m * sd);
        var lcl = mean - (3m * sd);
        var u2 = mean + (2m * sd);
        var l2 = mean - (2m * sd);
        var u1 = mean + sd;
        var l1 = mean - sd;

        var points = new List<SpcPoint>(n);
        for (var i = 0; i < n; i++)
        {
            var rules = new List<string>();
            var v = values[i];

            // R1 — beyond ±3σ.
            if (sd > 0m && (v > ucl || v < lcl))
            {
                rules.Add("R1");
            }

            // R2 — 2 of 3 consecutive beyond ±2σ on the same side (this point participates).
            if (sd > 0m && TwoOfThreeBeyond2Sigma(values, i, mean, u2, l2))
            {
                rules.Add("R2");
            }

            // R3 — 8 consecutive on the same side of the centre line, ending at this point.
            if (RunOnSameSide(values, i, mean, 8))
            {
                rules.Add("R3");
            }

            // R4 — 6 consecutive monotonic (increasing or decreasing), ending at this point.
            if (MonotonicRun(values, i, 6))
            {
                rules.Add("R4");
            }

            points.Add(new SpcPoint(i, v, rules.Count > 0, rules));
        }

        return new SpcAnalysis(true, decimal.Round(mean, 4), decimal.Round(sd, 4),
            decimal.Round(ucl, 4), decimal.Round(lcl, 4),
            decimal.Round(u2, 4), decimal.Round(l2, 4),
            decimal.Round(u1, 4), decimal.Round(l1, 4), points);
    }

    private static bool TwoOfThreeBeyond2Sigma(
        IReadOnlyList<decimal> values, int end, decimal mean, decimal upper2, decimal lower2)
    {
        if (end < 2)
        {
            return false;
        }

        var upper = 0;
        var lower = 0;
        for (var i = end - 2; i <= end; i++)
        {
            if (values[i] > upper2) { upper++; }
            else if (values[i] < lower2) { lower++; }
        }

        // The current point must itself be beyond ±2σ, so it is the one flagged.
        var current = values[end];
        return (upper >= 2 && current > upper2) || (lower >= 2 && current < lower2);
    }

    private static bool RunOnSameSide(IReadOnlyList<decimal> values, int end, decimal mean, int run)
    {
        if (end < run - 1)
        {
            return false;
        }

        var above = true;
        var below = true;
        for (var i = end - run + 1; i <= end; i++)
        {
            if (values[i] <= mean) { above = false; }
            if (values[i] >= mean) { below = false; }
        }

        return above || below;
    }

    private static bool MonotonicRun(IReadOnlyList<decimal> values, int end, int run)
    {
        if (end < run - 1)
        {
            return false;
        }

        var increasing = true;
        var decreasing = true;
        for (var i = end - run + 2; i <= end; i++)
        {
            if (values[i] <= values[i - 1]) { increasing = false; }
            if (values[i] >= values[i - 1]) { decreasing = false; }
        }

        return increasing || decreasing;
    }
}
