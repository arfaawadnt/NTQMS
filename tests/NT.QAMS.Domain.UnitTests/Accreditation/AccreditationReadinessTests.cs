using FluentAssertions;
using NT.QAMS.Domain.Accreditation;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Accreditation;

public class AccreditationReadinessTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static StandardSet ActiveSet(params (string Chapter, string Code, int Weight, ComplianceStatus Status)[] items)
    {
        var set = StandardSet.Define(AccreditationFramework.GAHAR, "GAHAR", "2024");
        var ids = new List<Guid>();
        foreach (var (chapter, code, weight, _) in items)
        {
            ids.Add(set.AddElement(chapter, chapter, chapter, code, "text", weight));
        }

        set.Activate();
        for (var i = 0; i < items.Length; i++)
        {
            set.AssessElement(ids[i], items[i].Status, null, Actor, Now);
        }

        return set;
    }

    [Fact]
    public void Overall_is_weight_weighted_compliance()
    {
        var set = ActiveSet(
            ("A", "A.1", 1, ComplianceStatus.Compliant),          // 1.0
            ("A", "A.2", 1, ComplianceStatus.PartiallyCompliant), // 0.5
            ("A", "A.3", 1, ComplianceStatus.NonCompliant));      // 0.0

        AccreditationReadiness.Overall(set.Elements).CompliancePercent.Should().Be(50m);
    }

    [Fact]
    public void Not_applicable_elements_are_excluded_from_the_denominator()
    {
        var set = ActiveSet(
            ("A", "A.1", 1, ComplianceStatus.Compliant),
            ("A", "A.2", 1, ComplianceStatus.NotApplicable));

        var overall = AccreditationReadiness.Overall(set.Elements);
        overall.CompliancePercent.Should().Be(100m, "the only applicable element is compliant");
        overall.ApplicableCount.Should().Be(1);
        overall.NotApplicableCount.Should().Be(1);
    }

    [Fact]
    public void Weighting_moves_the_figure_toward_heavier_elements()
    {
        var set = ActiveSet(
            ("A", "A.1", 9, ComplianceStatus.Compliant),     // heavy + compliant
            ("A", "A.2", 1, ComplianceStatus.NonCompliant)); // light + failing

        AccreditationReadiness.Overall(set.Elements).CompliancePercent.Should().Be(90m);
    }

    [Fact]
    public void By_chapter_groups_and_scores_independently()
    {
        var set = ActiveSet(
            ("A", "A.1", 1, ComplianceStatus.Compliant),
            ("B", "B.1", 1, ComplianceStatus.NonCompliant));

        var chapters = AccreditationReadiness.ByChapter(set.Elements);
        chapters.Should().HaveCount(2);
        chapters.Single(c => c.ChapterCode == "A").CompliancePercent.Should().Be(100m);
        chapters.Single(c => c.ChapterCode == "B").CompliancePercent.Should().Be(0m);
    }

    [Fact]
    public void A_wholly_not_applicable_or_empty_set_is_zero()
    {
        AccreditationReadiness.Overall([]).CompliancePercent.Should().Be(0m);
    }
}
