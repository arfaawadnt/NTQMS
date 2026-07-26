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
/// The QC acceptance limits (a controlled parameter set, F-16): the SD multiples
/// that define the warning, single-value rejection, and range rules, and the run
/// length for the shift rule. Defaults are the standard Westgard multi-rule
/// thresholds; a deployment can tune them via configuration without a code change
/// (e.g. a lab that runs 2-2.5s or an 8-x shift rule). Validated so a warning can
/// never sit above a rejection limit.
/// </summary>
public sealed record WestgardLimits(
    decimal WarningSd = 2m, decimal RejectSd = 3m, decimal RangeSd = 4m, int RunLength = 10)
{
    /// <summary>The canonical Westgard multi-rule thresholds (1-2s / 1-3s / R-4s / 10-x).</summary>
    public static readonly WestgardLimits Standard = new();

    /// <summary>Validates the limit set — used when limits arrive from configuration.</summary>
    public WestgardLimits Validated()
    {
        if (WarningSd <= 0m || RejectSd <= 0m || RangeSd <= 0m)
        {
            throw new SharedKernel.Primitives.DomainException(
                "QC-LIM-001", "Westgard SD limits must be positive.");
        }

        if (WarningSd >= RejectSd)
        {
            throw new SharedKernel.Primitives.DomainException(
                "QC-LIM-002", "The warning limit must be below the rejection limit.");
        }

        if (RunLength < 2)
        {
            throw new SharedKernel.Primitives.DomainException(
                "QC-LIM-003", "The shift-rule run length must be at least 2.");
        }

        return this;
    }
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
    /// <summary>The standard shift-rule run length (10-x). Retained for callers/tests that reference it.</summary>
    public const int TenXWindow = 10;

    public static WestgardVerdict Evaluate(
        decimal value, decimal mean, decimal sd, IReadOnlyList<decimal> priorValues, WestgardLimits? limits = null)
    {
        if (sd <= 0m)
        {
            throw new SharedKernel.Primitives.DomainException(
                "QC-SD", "Control SD must be positive to evaluate Westgard rules.");
        }

        var lim = limits ?? WestgardLimits.Standard;
        var z = (value - mean) / sd;
        var priorZ = priorValues.Select(v => (v - mean) / sd).ToList();

        var violations = new List<string>();

        // 1-3s: single value beyond the rejection limit (±RejectSd).
        if (Math.Abs(z) > lim.RejectSd)
        {
            violations.Add($"1-{lim.RejectSd:0.#}s");
        }

        // 2-2s: this value and the immediately prior one both beyond the SAME ±WarningSd limit.
        if (priorZ.Count >= 1 && Math.Abs(z) > lim.WarningSd)
        {
            var prev = priorZ[^1];
            if (Math.Abs(prev) > lim.WarningSd && Math.Sign(prev) == Math.Sign(z))
            {
                violations.Add($"2-{lim.WarningSd:0.#}s");
            }
        }

        // R-4s: this value and the immediately prior one span more than RangeSd (opposite sides).
        if (priorZ.Count >= 1)
        {
            var prev = priorZ[^1];
            if (Math.Abs(z - prev) > lim.RangeSd)
            {
                violations.Add($"R-{lim.RangeSd:0.#}s");
            }
        }

        // Shift rule (10-x): RunLength consecutive values all on the same side of the mean.
        if (priorZ.Count >= lim.RunLength - 1)
        {
            var window = priorZ.Skip(priorZ.Count - (lim.RunLength - 1)).Append(z).ToList();
            if (window.All(x => x > 0m) || window.All(x => x < 0m))
            {
                violations.Add($"{lim.RunLength}-x");
            }
        }

        if (violations.Count > 0)
        {
            return new WestgardVerdict(WestgardOutcome.OutOfControl, violations);
        }

        // 1-2s is warning-only, and only relevant when no rejection rule fired.
        if (Math.Abs(z) > lim.WarningSd)
        {
            return new WestgardVerdict(WestgardOutcome.Warning, [$"1-{lim.WarningSd:0.#}s"]);
        }

        return WestgardVerdict.InControl;
    }
}
