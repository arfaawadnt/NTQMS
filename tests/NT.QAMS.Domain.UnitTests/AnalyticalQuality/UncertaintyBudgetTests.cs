using FluentAssertions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.AnalyticalQuality;

public sealed class UncertaintyBudgetTests
{
    private static readonly Guid Qm = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static UncertaintyBudget NewBudget(decimal? target = 10m) => UncertaintyBudget.Create(
        "MU-2026-0001", "Glucose", "Hexokinase (Cobas c503)", "mmol/L", "5.5 mmol/L",
        coverageFactor: 2m, targetExpandedUncertainty: target);

    [Fact]
    public void Combined_uncertainty_is_root_sum_of_squares_and_expansion_applies_k()
    {
        var budget = NewBudget();
        budget.AddComponent("Repeatability (QC CV)", UncertaintyComponentType.TypeA, 3m, "QC lot 77, 6 months");
        budget.AddComponent("Bias (PT)", UncertaintyComponentType.TypeB, 4m, "EQAS 2026-A/B");

        budget.Calculate();

        // √(3² + 4²) = 5; U = k·u_c = 2·5 = 10.
        budget.CombinedStandardUncertainty.Should().Be(5m);
        budget.ExpandedUncertainty.Should().Be(10m);
        budget.MeetsTarget.Should().BeTrue(); // U = 10 ≤ target 10
        budget.Status.Should().Be(UncertaintyBudgetStatus.Calculated);
    }

    [Fact]
    public void Exceeding_the_target_fails_the_verdict_and_no_target_gives_no_verdict()
    {
        var failing = NewBudget(target: 9m);
        failing.AddComponent("Repeatability", UncertaintyComponentType.TypeA, 3m, null);
        failing.AddComponent("Bias", UncertaintyComponentType.TypeB, 4m, null);
        failing.Calculate();
        failing.MeetsTarget.Should().BeFalse();

        var noTarget = NewBudget(target: null);
        noTarget.AddComponent("Repeatability", UncertaintyComponentType.TypeA, 3m, null);
        noTarget.Calculate();
        noTarget.MeetsTarget.Should().BeNull();
    }

    [Fact]
    public void Editing_components_invalidates_a_prior_calculation()
    {
        var budget = NewBudget();
        budget.AddComponent("Repeatability", UncertaintyComponentType.TypeA, 3m, null);
        budget.Calculate();

        budget.AddComponent("Calibrator", UncertaintyComponentType.TypeB, 1m, "Certificate");

        budget.Status.Should().Be(UncertaintyBudgetStatus.Draft);
        budget.ExpandedUncertainty.Should().BeNull();
    }

    [Fact]
    public void Approval_requires_a_calculation_and_freezes_the_budget()
    {
        var budget = NewBudget();
        budget.AddComponent("Repeatability", UncertaintyComponentType.TypeA, 3m, null);

        var early = () => budget.Approve(Qm, Now);
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MU-010");

        budget.Calculate();
        budget.Approve(Qm, Now);

        budget.Status.Should().Be(UncertaintyBudgetStatus.Approved);
        budget.DomainEvents.Should().ContainSingle(e => e is UncertaintyBudgetApproved);

        var mutate = () => budget.AddComponent("Late", UncertaintyComponentType.TypeB, 1m, null);
        mutate.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("MU-011");
    }

    [Fact]
    public void Calculation_demands_at_least_one_component_and_valid_inputs()
    {
        var empty = NewBudget();
        var act = () => empty.Calculate();
        act.Should().Throw<DomainException>().Which.Code.Should().Be("MU-007");

        var badK = () => UncertaintyBudget.Create("MU-1", "A", "M", "u", "L", 5m, null);
        badK.Should().Throw<DomainException>().Which.Code.Should().Be("MU-002");

        var negative = () => NewBudget().AddComponent("X", UncertaintyComponentType.TypeA, -1m, null);
        negative.Should().Throw<DomainException>().Which.Code.Should().Be("MU-005");
    }
}
