namespace NT.QAMS.Domain.AnalyticalQuality;

public enum WestgardOutcome { InControl, Warning, OutOfControl }

/// <summary>
/// The verdict for a single QC run: the outcome plus the rules that fired.
/// Rule codes are the record of fact stored on the run (never recomputed —
/// window statistics could change as later runs arrive; the verdict at entry
/// time is what the analyst acted on).
/// </summary>
public sealed record WestgardVerdict(WestgardOutcome Outcome, IReadOnlyList<string> ViolatedRules)
{
    public static readonly WestgardVerdict InControl = new(WestgardOutcome.InControl, []);
}

/// <summary>
/// Evaluates Westgard multi-rule QC (Levey-Jennings) for one new control value
/// against a profile's target mean/SD and a window of prior values (oldest first).
///
/// Rejection rules: 1-3s (one value beyond ±3SD), 2-2s (two consecutive beyond
/// the same ±2SD limit), R-4s (consecutive pair spanning more than 4SD),
/// 10-x (ten consecutive on the same side of the mean).
/// Warning rule: 1-2s (one value beyond ±2SD) — flags but does not reject.
///
/// Pure function: no I/O, no state — trivially and exhaustively testable.
/// </summary>
public static class WestgardEvaluator
{
    public const int TenXWindow = 10;

    public static WestgardVerdict Evaluate(decimal value, decimal mean, decimal sd, IReadOnlyList<decimal> priorValues)
    {
        if (sd <= 0m)
        {
            throw new SharedKernel.Primitives.DomainException(
                "QC-SD", "Control SD must be positive to evaluate Westgard rules.");
        }

        var z = (value - mean) / sd;
        var priorZ = priorValues.Select(v => (v - mean) / sd).ToList();

        var violations = new List<string>();

        // 1-3s: single value beyond ±3SD.
        if (Math.Abs(z) > 3m)
        {
            violations.Add("1-3s");
        }

        // 2-2s: this value and the immediately prior one both beyond the SAME ±2SD limit.
        if (priorZ.Count >= 1 && Math.Abs(z) > 2m)
        {
            var prev = priorZ[^1];
            if (Math.Abs(prev) > 2m && Math.Sign(prev) == Math.Sign(z))
            {
                violations.Add("2-2s");
            }
        }

        // R-4s: this value and the immediately prior one span more than 4SD (opposite sides).
        if (priorZ.Count >= 1)
        {
            var prev = priorZ[^1];
            if (Math.Abs(z - prev) > 4m)
            {
                violations.Add("R-4s");
            }
        }

        // 10-x: ten consecutive values (this + last 9) all on the same side of the mean.
        if (priorZ.Count >= TenXWindow - 1)
        {
            var window = priorZ.Skip(priorZ.Count - (TenXWindow - 1)).Append(z).ToList();
            if (window.All(x => x > 0m) || window.All(x => x < 0m))
            {
                violations.Add("10-x");
            }
        }

        if (violations.Count > 0)
        {
            return new WestgardVerdict(WestgardOutcome.OutOfControl, violations);
        }

        // 1-2s is warning-only, and only relevant when no rejection rule fired.
        if (Math.Abs(z) > 2m)
        {
            return new WestgardVerdict(WestgardOutcome.Warning, ["1-2s"]);
        }

        return WestgardVerdict.InControl;
    }
}
