using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum SigmaAssessmentState { Draft, SignedOff }

/// <summary>Six-Sigma performance grade band for an analytical method.</summary>
public enum SigmaGrade { Unacceptable, Marginal, Good, Excellent, WorldClass }

/// <summary>
/// Analytical Six-Sigma assessment: from the allowable total error and the
/// method's observed bias and imprecision it derives the sigma metric
/// σ = (TEa% − |bias%|) / CV%, its performance grade, and — because QC design
/// should scale to method capability — the Westgard sigma-based QC
/// recommendation (which rules, how many controls). Inputs are editable while
/// Draft; sign-off freezes the assessment. All outputs are derivable-only.
/// </summary>
public sealed class SigmaAssessment : AggregateRoot, ITenantScoped
{
    private SigmaAssessment()
    {
        AssessmentRef = null!;
        Analyte = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }
    public string AssessmentRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    /// <summary>Allowable total error as a percentage (the quality requirement, TEa%).</summary>
    public decimal AllowableTotalErrorPct { get; private set; }
    /// <summary>Observed bias as a percentage (signed; magnitude is used).</summary>
    public decimal BiasPct { get; private set; }
    /// <summary>Observed imprecision as a coefficient of variation (%).</summary>
    public decimal CvPct { get; private set; }
    public SigmaAssessmentState State { get; private set; }

    // Derived.
    public decimal SigmaValue { get; private set; }
    public SigmaGrade Grade { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public static SigmaAssessment Create(
        string assessmentRef, string analyte, string unit,
        decimal allowableTotalErrorPct, decimal biasPct, decimal cvPct)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("SIG-001", "An analyte is required.");
        }

        var assessment = new SigmaAssessment
        {
            AssessmentRef = assessmentRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            State = SigmaAssessmentState.Draft,
        };
        assessment.SetInputs(allowableTotalErrorPct, biasPct, cvPct);
        return assessment;
    }

    /// <summary>Re-enters the inputs and recomputes σ and the grade (Draft only).</summary>
    public void SetInputs(decimal allowableTotalErrorPct, decimal biasPct, decimal cvPct)
    {
        if (State != SigmaAssessmentState.Draft)
        {
            throw new InvalidStateTransitionException("SIG-010", "A signed-off assessment is immutable.");
        }

        if (allowableTotalErrorPct <= 0m)
        {
            throw new DomainException("SIG-002", "The allowable total error must be a positive percentage.");
        }

        if (cvPct <= 0m)
        {
            throw new DomainException("SIG-003", "The CV must be a positive percentage — sigma is undefined at zero imprecision.");
        }

        AllowableTotalErrorPct = allowableTotalErrorPct;
        BiasPct = biasPct;
        CvPct = cvPct;

        // σ = (TEa − |bias|) / CV. A negative numerator (bias alone exceeds TEa)
        // floors at 0 — the method is already unacceptable.
        var numerator = allowableTotalErrorPct - Math.Abs(biasPct);
        SigmaValue = numerator <= 0m ? 0m : Math.Round(numerator / cvPct, 2);
        Grade = GradeFor(SigmaValue);
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001");
        if (State != SigmaAssessmentState.Draft)
        {
            throw new InvalidStateTransitionException("SIG-011", "The assessment is already signed off.");
        }

        State = SigmaAssessmentState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new SigmaAssessmentSignedOff(Id, AssessmentRef, Analyte, SigmaValue, TenantId));
    }

    /// <summary>
    /// Westgard sigma-based QC design: higher-capability methods need fewer
    /// rules and controls; a sub-3-sigma method needs maximal QC or replacement.
    /// </summary>
    public string QcRecommendation => SigmaValue switch
    {
        >= 6m => "1:3s, N=2, R=1 — a single rule with minimal QC (world-class capability).",
        >= 5m => "1:3s / 2:2s / R:4s, N=2, R=1 — a short multirule.",
        >= 4m => "1:3s / 2:2s / R:4s / 4:1s, N=4, R=1 — full multirule.",
        >= 3m => "1:3s / 2:2s / R:4s / 4:1s / 8:x, N=6 — maximum multirule QC.",
        _ => "Below 3σ — the method does not meet the minimum; review the process or replace the method.",
    };

    private static SigmaGrade GradeFor(decimal sigma) => sigma switch
    {
        >= 6m => SigmaGrade.WorldClass,
        >= 5m => SigmaGrade.Excellent,
        >= 4m => SigmaGrade.Good,
        >= 3m => SigmaGrade.Marginal,
        _ => SigmaGrade.Unacceptable,
    };
}

public sealed record SigmaAssessmentSignedOff(
    Guid AssessmentId, string AssessmentRef, string Analyte, decimal SigmaValue, Guid TenantId) : DomainEvent;
