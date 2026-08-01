using FluentAssertions;
using NT.QAMS.Domain.Reporting;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Reporting;

/// <summary>
/// The Quality Health Score weighting (ISO 17025 §8.9.2 management-review input).
/// The weighting defines a governance figure reported to management, so these
/// tests pin the two things that make the figure trustworthy: it cannot be left
/// undefined, and it cannot be changed without a recorded reason.
/// </summary>
public sealed class QualityHealthProfileTests
{
    private const string Reason = "Risk weighted higher for the 2026 review cycle.";

    private static Dictionary<QualityHealthCategory, int> Even(int weight = 10) =>
        Enum.GetValues<QualityHealthCategory>().ToDictionary(c => c, _ => weight);

    [Fact]
    public void A_new_profile_weights_every_category_equally()
    {
        var profile = QualityHealthProfile.CreateDefault();

        profile.Weights.Should().HaveCount(Enum.GetValues<QualityHealthCategory>().Length);
        profile.Weights.Should().OnlyContain(w => w.Weight == QualityHealthProfile.DefaultWeight);
    }

    [Fact]
    public void Replacing_the_weights_records_the_before_and_after_of_each_change()
    {
        var profile = QualityHealthProfile.CreateDefault();
        var weights = Even();
        weights[QualityHealthCategory.Risk] = 30;

        profile.ReplaceWeights(weights, Reason);

        profile.WeightFor(QualityHealthCategory.Risk).Should().Be(30);
        var raised = profile.DomainEvents.OfType<QualityHealthWeightsChanged>().Single();
        // The trail must show what the score meant before and after, not merely
        // that the definition was edited.
        raised.Changes.Should().ContainSingle().Which.Should().Be("Risk:10→30");
        raised.Reason.Should().Be(Reason);
    }

    [Fact]
    public void An_unchanged_weighting_raises_nothing()
    {
        var profile = QualityHealthProfile.CreateDefault();

        profile.ReplaceWeights(Even(), Reason);

        // A no-op edit is not a change to the quality record; recording one would
        // dilute the trail with events that carry no information.
        profile.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_change_without_a_reason_is_refused()
    {
        var profile = QualityHealthProfile.CreateDefault();
        var weights = Even();
        weights[QualityHealthCategory.Risk] = 25;

        var act = () => profile.ReplaceWeights(weights, "  ");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("QHP-001");
    }

    [Fact]
    public void A_partial_weighting_is_refused()
    {
        var profile = QualityHealthProfile.CreateDefault();
        var partial = Even();
        partial.Remove(QualityHealthCategory.Complaints);

        var act = () => profile.ReplaceWeights(partial, Reason);

        // Omitting a category would leave the caller guessing what it now weighs.
        act.Should().Throw<DomainException>().Which.Code.Should().Be("QHP-002");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_weight_outside_the_permitted_range_is_refused(int weight)
    {
        var profile = QualityHealthProfile.CreateDefault();
        var weights = Even();
        weights[QualityHealthCategory.Risk] = weight;

        var act = () => profile.ReplaceWeights(weights, Reason);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("QHP-003");
    }

    [Fact]
    public void An_all_zero_weighting_is_refused()
    {
        var profile = QualityHealthProfile.CreateDefault();

        var act = () => profile.ReplaceWeights(Even(0), Reason);

        // An all-zero profile would make the composite undefined while the page
        // still appeared to report one.
        act.Should().Throw<DomainException>().Which.Code.Should().Be("QHP-004");
    }

    [Fact]
    public void A_single_category_may_be_excluded_by_zeroing_it()
    {
        var profile = QualityHealthProfile.CreateDefault();
        var weights = Even();
        weights[QualityHealthCategory.SupplierQuality] = 0;

        profile.ReplaceWeights(weights, Reason);

        profile.WeightFor(QualityHealthCategory.SupplierQuality).Should().Be(0);
    }
}
