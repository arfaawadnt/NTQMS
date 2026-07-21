using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum PtPerformance { Pending, Satisfactory, Questionable, Unsatisfactory }

/// <summary>
/// Proficiency-testing / interlaboratory-comparison enrollment. Recording a
/// result computes the z-score and performance category; an unsatisfactory
/// result raises PtUnsatisfactory, which the Improvement context turns into an NC
/// (cross-module saga, same shape as audit findings).
/// </summary>
public sealed class PtEnrollment : AggregateRoot, ITenantScoped
{
    public const decimal QuestionableThreshold = 2m;
    public const decimal UnsatisfactoryThreshold = 3m;

    private PtEnrollment()
    {
        PtRef = null!;
        Scheme = null!;
        Analyte = null!;
    }

    public Guid TenantId { get; set; }
    public string PtRef { get; private set; }
    public string Scheme { get; private set; }
    public string Analyte { get; private set; }
    public string Cycle { get; private set; } = string.Empty;
    public decimal? SubmittedValue { get; private set; }
    public decimal? AssignedValue { get; private set; }
    public decimal? StandardDeviation { get; private set; }
    public decimal? ZScore { get; private set; }
    public PtPerformance Performance { get; private set; }

    public static PtEnrollment Enroll(string ptRef, string scheme, string analyte, string cycle)
    {
        if (string.IsNullOrWhiteSpace(scheme) || string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("PT-001", "PT scheme and analyte are required.");
        }

        return new PtEnrollment
        {
            PtRef = ptRef,
            Scheme = scheme.Trim(),
            Analyte = analyte.Trim(),
            Cycle = cycle?.Trim() ?? string.Empty,
            Performance = PtPerformance.Pending,
        };
    }

    /// <summary>
    /// Records the result. z = (submitted − assigned) / SD; |z| ≤ 2 satisfactory,
    /// 2 &lt; |z| &lt; 3 questionable, |z| ≥ 3 unsatisfactory.
    /// </summary>
    public void RecordResult(decimal submitted, decimal assigned, decimal standardDeviation, Guid raisedBy)
    {
        if (Performance != PtPerformance.Pending)
        {
            throw new InvalidStateTransitionException("PT-010", "A PT result has already been recorded.");
        }

        if (standardDeviation <= 0m)
        {
            throw new DomainException("PT-011", "The scheme standard deviation must be positive.");
        }

        SubmittedValue = submitted;
        AssignedValue = assigned;
        StandardDeviation = standardDeviation;
        var z = (submitted - assigned) / standardDeviation;
        ZScore = Math.Round(z, 3);

        var absZ = Math.Abs(z);
        Performance = absZ >= UnsatisfactoryThreshold ? PtPerformance.Unsatisfactory
            : absZ > QuestionableThreshold ? PtPerformance.Questionable
            : PtPerformance.Satisfactory;

        if (Performance == PtPerformance.Unsatisfactory)
        {
            Raise(new PtUnsatisfactory(Id, PtRef, Analyte, ZScore.Value, TenantId, raisedBy));
        }
    }
}

public sealed record PtUnsatisfactory(
    Guid PtId, string PtRef, string Analyte, decimal ZScore, Guid TenantId, Guid RaisedBy) : DomainEvent;
