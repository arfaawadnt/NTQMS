using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

/// <summary>
/// A QC control profile: analyte + instrument + control level/lot, with the
/// target mean/SD and effective dating. Runs reference it by id; the profile is
/// deliberately separate from the high-volume run stream (write-throughput).
/// Target changes are effective-dated (forward-only) so historical verdicts stand.
/// </summary>
public sealed class QcProfile : AggregateRoot, ITenantScoped
{
    private QcProfile()
    {
        Analyte = null!;
        Instrument = null!;
        ControlLot = null!;
    }

    public Guid TenantId { get; set; }
    public string Analyte { get; private set; }
    public string Instrument { get; private set; }
    public string ControlLot { get; private set; }
    public decimal TargetMean { get; private set; }
    public decimal TargetSd { get; private set; }
    public bool IsActive { get; private set; }

    public static QcProfile Create(
        string analyte, string instrument, string controlLot, decimal targetMean, decimal targetSd)
    {
        if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(instrument))
        {
            throw new DomainException("QC-001", "Analyte and instrument are required.");
        }

        if (targetSd <= 0m)
        {
            throw new DomainException("QC-002", "Target SD must be positive.");
        }

        return new QcProfile
        {
            Analyte = analyte.Trim(),
            Instrument = instrument.Trim(),
            ControlLot = string.IsNullOrWhiteSpace(controlLot) ? "N/A" : controlLot.Trim(),
            TargetMean = targetMean,
            TargetSd = targetSd,
            IsActive = true,
        };
    }

    public void UpdateTargets(decimal targetMean, decimal targetSd)
    {
        if (targetSd <= 0m)
        {
            throw new DomainException("QC-002", "Target SD must be positive.");
        }

        TargetMean = targetMean;
        TargetSd = targetSd;
    }

    public void Deactivate() => IsActive = false;
}

/// <summary>
/// A single control measurement. Its Westgard verdict is computed once at entry
/// (by the application, via <see cref="WestgardEvaluator"/>) and stored as the
/// record of fact. Out-of-control runs require a troubleshooting note before the
/// analyte's result release resumes (release gate lives at the LIMS boundary,
/// out of scope; the event is this context's contract).
/// </summary>
public sealed class QcRun : AggregateRoot, ITenantScoped
{
    private QcRun()
    {
        Operator = null!;
        Outcome = null!;
        ViolatedRules = null!;
    }

    public Guid TenantId { get; set; }
    public Guid ProfileId { get; private set; }
    public decimal Value { get; private set; }
    public decimal ZScore { get; private set; }
    public string Outcome { get; private set; }
    public string ViolatedRules { get; private set; }
    public string Operator { get; private set; }
    public DateTimeOffset MeasuredAtUtc { get; private set; }
    public string? TroubleshootingNote { get; private set; }

    public static QcRun Record(
        Guid profileId, decimal value, decimal zScore, WestgardVerdict verdict,
        string @operator, DateTimeOffset measuredAt)
    {
        var run = new QcRun
        {
            ProfileId = profileId,
            Value = value,
            ZScore = Math.Round(zScore, 3),
            Outcome = verdict.Outcome.ToString(),
            ViolatedRules = string.Join(",", verdict.ViolatedRules),
            Operator = string.IsNullOrWhiteSpace(@operator) ? "unknown" : @operator.Trim(),
            MeasuredAtUtc = measuredAt,
        };

        if (verdict.Outcome == WestgardOutcome.OutOfControl)
        {
            run.Raise(new QcOutOfControl(run.Id, profileId, run.ViolatedRules, run.TenantId));
        }

        return run;
    }

    public void LogTroubleshooting(string note)
    {
        if (Outcome != WestgardOutcome.OutOfControl.ToString())
        {
            throw new InvalidStateTransitionException(
                "QC-010", "Troubleshooting notes apply only to out-of-control runs.");
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException("QC-011", "A troubleshooting note is required.");
        }

        TroubleshootingNote = note.Trim();
    }
}

public sealed record QcOutOfControl(Guid RunId, Guid ProfileId, string ViolatedRules, Guid TenantId) : DomainEvent;
