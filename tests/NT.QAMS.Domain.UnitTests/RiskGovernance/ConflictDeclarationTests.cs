using FluentAssertions;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.RiskGovernance;

public sealed class ConflictDeclarationTests
{
    private static readonly Guid Declarant = Guid.CreateVersion7();
    private static readonly Guid Qm = Guid.CreateVersion7();

    private static ConflictDeclaration Declared() => ConflictDeclaration.Declare(
        "COI-2026-0001", Declarant, "Spouse is sales manager at reagent supplier X", "Supplier X",
        new DateOnly(2026, 7, 25));

    [Fact]
    public void Declarants_cannot_assess_their_own_conflict()
    {
        var conflict = Declared();
        var selfAssess = () => conflict.Assess(Declarant, ConflictRiskLevel.Low, "None needed");
        selfAssess.Should().Throw<DomainException>().Which.Code.Should().Be("SOD-COI-001");
    }

    [Fact]
    public void High_risk_assessment_raises_the_impartiality_event()
    {
        var conflict = Declared();
        conflict.Assess(Qm, ConflictRiskLevel.High, "Excluded from supplier X evaluations and PO approvals.");

        conflict.Status.Should().Be(ConflictStatus.Assessed);
        conflict.DomainEvents.Should().ContainSingle(e => e is HighImpartialityRiskDeclared);

        var low = Declared();
        low.Assess(Qm, ConflictRiskLevel.Low, "No procurement involvement.");
        low.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Closure_requires_assessment_first_and_a_note()
    {
        var conflict = Declared();
        var early = () => conflict.Close(ConflictOutcome.Mitigated, "done");
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("COI-012");

        conflict.Assess(Qm, ConflictRiskLevel.Medium, "Dual review on affected POs.");
        var blank = () => conflict.Close(ConflictOutcome.Mitigated, " ");
        blank.Should().Throw<DomainException>().Which.Code.Should().Be("COI-013");

        conflict.Close(ConflictOutcome.Mitigated, "Mitigation in force; re-declare annually.");
        conflict.Status.Should().Be(ConflictStatus.Closed);
    }
}
