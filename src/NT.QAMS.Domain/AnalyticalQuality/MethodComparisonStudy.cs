using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum MethodComparisonState { DataEntry, Calculated, SignedOff }

/// <summary>One paired measurement: reference method (X) vs test/candidate method (Y).</summary>
public sealed class MeasurementPair : Entity
{
    internal MeasurementPair(decimal referenceValue, decimal testValue, string? sampleId)
    {
        ReferenceValue = referenceValue;
        TestValue = testValue;
        SampleId = sampleId;
    }

    private MeasurementPair() { }

    public decimal ReferenceValue { get; private set; }
    public decimal TestValue { get; private set; }
    public string? SampleId { get; private set; }
}

/// <summary>
/// Method-comparison / patient-sample comparability study (CLSI EP09): pairs of
/// results from a reference method (X) and a candidate method (Y). On Calculate
/// the aggregate derives, from the paired data alone, Deming and Passing–Bablok
/// regression (slope + intercept), the Pearson correlation, and the Bland–Altman
/// agreement (mean bias and 95% limits of agreement). Pairs are immutable
/// evidence (void-and-re-enter, never edit-in-place); statistics are
/// derivable-only and re-editing the data invalidates them; sign-off freezes
/// the study.
/// </summary>
public sealed class MethodComparisonStudy : AggregateRoot, ITenantScoped
{
    /// <summary>EP09 recommends at least 40 patient samples; below this the study is flagged underpowered.</summary>
    public const int RecommendedMinimumPairs = 40;

    private readonly List<MeasurementPair> _pairs = [];

    private MethodComparisonStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
        ReferenceMethod = null!;
        TestMethod = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    public string ReferenceMethod { get; private set; }
    public string TestMethod { get; private set; }
    public MethodComparisonState State { get; private set; }

    // Derived statistics (null until Calculate).
    public int? PairCount { get; private set; }
    public decimal? PearsonR { get; private set; }
    public decimal? DemingSlope { get; private set; }
    public decimal? DemingIntercept { get; private set; }
    public decimal? PassingBablokSlope { get; private set; }
    public decimal? PassingBablokIntercept { get; private set; }
    public decimal? MeanBias { get; private set; }
    public decimal? BiasSd { get; private set; }
    public decimal? LimitOfAgreementLower { get; private set; }
    public decimal? LimitOfAgreementUpper { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<MeasurementPair> Pairs => _pairs.AsReadOnly();

    public static MethodComparisonStudy Configure(
        string studyRef, string analyte, string unit, string referenceMethod, string testMethod)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("MC-001", "An analyte is required.");
        }

        if (string.IsNullOrWhiteSpace(referenceMethod) || string.IsNullOrWhiteSpace(testMethod))
        {
            throw new DomainException("MC-002", "Both the reference method (X) and the test method (Y) are required.");
        }

        return new MethodComparisonStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            ReferenceMethod = referenceMethod.Trim(),
            TestMethod = testMethod.Trim(),
            State = MethodComparisonState.DataEntry,
        };
    }

    public Guid AddPair(decimal referenceValue, decimal testValue, string? sampleId)
    {
        RequireEditable();
        if (referenceValue <= 0m || testValue <= 0m)
        {
            throw new DomainException("MC-003", "Measured values must be positive.");
        }

        var pair = new MeasurementPair(referenceValue, testValue,
            string.IsNullOrWhiteSpace(sampleId) ? null : sampleId.Trim());
        _pairs.Add(pair);
        Invalidate();
        return pair.Id;
    }

    public void RemovePair(Guid pairId)
    {
        RequireEditable();
        var pair = _pairs.FirstOrDefault(p => p.Id == pairId)
            ?? throw new DomainException("MC-404", "Measurement pair not found.");
        _pairs.Remove(pair);
        Invalidate();
    }

    /// <summary>
    /// Recomputes every statistic from the paired data. Ordinary Deming
    /// (error-variance ratio λ = 1) and Passing–Bablok are used together so a
    /// reviewer can compare a parametric and a non-parametric fit.
    /// </summary>
    public void Calculate()
    {
        RequireEditable();
        if (_pairs.Count < 2)
        {
            throw new DomainException("MC-010", "At least two measurement pairs are required to fit a regression.");
        }

        var x = _pairs.Select(p => (double)p.ReferenceValue).ToArray();
        var y = _pairs.Select(p => (double)p.TestValue).ToArray();
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

        if (sxx == 0 || syy == 0)
        {
            throw new DomainException("MC-011", "The reference or test values show no spread — a regression cannot be fitted.");
        }

        PearsonR = Round(sxy / Math.Sqrt(sxx * syy));

        // Ordinary Deming regression (λ = 1).
        const double lambda = 1.0;
        var demingSlope = (syy - lambda * sxx + Math.Sqrt(Math.Pow(syy - lambda * sxx, 2) + 4 * lambda * sxy * sxy))
                          / (2 * sxy);
        DemingSlope = Round(demingSlope);
        DemingIntercept = Round(meanY - demingSlope * meanX);

        var (pbSlope, pbIntercept) = PassingBablok(x, y);
        PassingBablokSlope = Round(pbSlope);
        PassingBablokIntercept = Round(pbIntercept);

        // Bland–Altman agreement on the differences (test − reference).
        var diffs = new double[n];
        for (var i = 0; i < n; i++)
        {
            diffs[i] = y[i] - x[i];
        }

        var meanDiff = diffs.Average();
        var variance = n > 1 ? diffs.Sum(d => (d - meanDiff) * (d - meanDiff)) / (n - 1) : 0;
        var sd = Math.Sqrt(variance);
        MeanBias = Round(meanDiff);
        BiasSd = Round(sd);
        LimitOfAgreementLower = Round(meanDiff - 1.96 * sd);
        LimitOfAgreementUpper = Round(meanDiff + 1.96 * sd);

        PairCount = n;
        State = MethodComparisonState.Calculated;
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        EnsureSignerIsNotPreparer(actorId, "SOD-AQ-001");
        if (State != MethodComparisonState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "MC-012", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = MethodComparisonState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new MethodComparisonSignedOff(Id, StudyRef, Analyte, TenantId));
    }

    /// <summary>Whether the study meets the EP09 sample-count recommendation.</summary>
    public bool MeetsRecommendedPower => _pairs.Count >= RecommendedMinimumPairs;

    /// <summary>
    /// Passing–Bablok non-parametric regression: slope is the shifted median of
    /// all pairwise slopes (shift K = count of slopes below −1); intercept is
    /// the median of (yᵢ − slope·xᵢ). Vertical pairs (equal X) are excluded.
    /// </summary>
    private static (double Slope, double Intercept) PassingBablok(double[] x, double[] y)
    {
        var slopes = new List<double>();
        for (var i = 0; i < x.Length; i++)
        {
            for (var j = i + 1; j < x.Length; j++)
            {
                if (x[i] == x[j])
                {
                    continue;
                }

                slopes.Add((y[j] - y[i]) / (x[j] - x[i]));
            }
        }

        if (slopes.Count == 0)
        {
            throw new DomainException("MC-011", "No comparable pairs (all reference values are identical).");
        }

        slopes.Sort();
        var count = slopes.Count;
        var k = slopes.Count(s => s < -1.0);
        double slope;
        if (count % 2 == 1)
        {
            slope = slopes[(count - 1) / 2 + k];
        }
        else
        {
            slope = (slopes[count / 2 + k - 1] + slopes[count / 2 + k]) / 2.0;
        }

        var intercepts = new double[x.Length];
        for (var i = 0; i < x.Length; i++)
        {
            intercepts[i] = y[i] - slope * x[i];
        }

        return (slope, Median(intercepts));
    }

    private static double Median(double[] values)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var m = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2.0;
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private void Invalidate()
    {
        PairCount = null;
        PearsonR = null;
        DemingSlope = DemingIntercept = null;
        PassingBablokSlope = PassingBablokIntercept = null;
        MeanBias = BiasSd = LimitOfAgreementLower = LimitOfAgreementUpper = null;
        if (State == MethodComparisonState.Calculated)
        {
            State = MethodComparisonState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == MethodComparisonState.SignedOff)
        {
            throw new InvalidStateTransitionException("MC-013", "A signed-off study is immutable.");
        }
    }
}

public sealed record MethodComparisonSignedOff(
    Guid StudyId, string StudyRef, string Analyte, Guid TenantId) : DomainEvent;
