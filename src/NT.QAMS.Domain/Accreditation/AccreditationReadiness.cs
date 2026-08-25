namespace NT.QAMS.Domain.Accreditation;

/// <summary>Readiness for one chapter (or the whole set): weighted compliance and its gap counts.</summary>
public sealed record ReadinessScore(
    string ChapterCode, string ChapterTitle,
    int ElementCount, int ApplicableCount, int CompliantCount, int PartialCount,
    int NonCompliantCount, int NotAssessedCount, int NotApplicableCount,
    decimal CompliancePercent);

/// <summary>
/// Computes accreditation readiness from a set's self-assessed elements (HQMS M07). Each
/// element scores Compliant = 1, PartiallyCompliant = 0.5, everything else = 0; NotApplicable
/// elements are excluded from the denominator entirely. The percentage is weighted by each
/// element's weight so surveyor-critical elements move the figure more. Pure and total —
/// an empty or wholly not-applicable set yields 0%.
/// </summary>
public static class AccreditationReadiness
{
    /// <summary>The compliance weight of a single verdict.</summary>
    public static decimal Score(ComplianceStatus status) => status switch
    {
        ComplianceStatus.Compliant => 1m,
        ComplianceStatus.PartiallyCompliant => 0.5m,
        _ => 0m,
    };

    public static ReadinessScore Overall(IReadOnlyList<StandardElement> elements) =>
        Aggregate("*", "Overall", elements);

    public static IReadOnlyList<ReadinessScore> ByChapter(IReadOnlyList<StandardElement> elements) =>
        elements
            .GroupBy(e => new { e.ChapterCode, e.ChapterTitle })
            .OrderBy(g => g.Key.ChapterCode)
            .Select(g => Aggregate(g.Key.ChapterCode, g.Key.ChapterTitle, g.ToList()))
            .ToList();

    private static ReadinessScore Aggregate(string code, string title, IReadOnlyList<StandardElement> elements)
    {
        var applicable = elements.Where(e => e.ComplianceStatus != ComplianceStatus.NotApplicable).ToList();
        var weightSum = applicable.Sum(e => e.Weight);
        var weighted = applicable.Sum(e => e.Weight * Score(e.ComplianceStatus));
        var percent = weightSum == 0 ? 0m : decimal.Round(weighted * 100m / weightSum, 1);

        return new ReadinessScore(
            code, title,
            elements.Count,
            applicable.Count,
            elements.Count(e => e.ComplianceStatus == ComplianceStatus.Compliant),
            elements.Count(e => e.ComplianceStatus == ComplianceStatus.PartiallyCompliant),
            elements.Count(e => e.ComplianceStatus == ComplianceStatus.NonCompliant),
            elements.Count(e => e.ComplianceStatus == ComplianceStatus.NotAssessed),
            elements.Count(e => e.ComplianceStatus == ComplianceStatus.NotApplicable),
            percent);
    }
}
