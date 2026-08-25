using FluentAssertions;
using NT.QAMS.Domain.Accreditation;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Accreditation;

public class StandardSetTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static StandardSet DraftWithElement(out Guid elementId)
    {
        var set = StandardSet.Define(AccreditationFramework.GAHAR, "GAHAR Hospital", "2024");
        elementId = set.AddElement("PC", "Patient-Centred Care", "PC.1", "PC.1.1", "Patients are identified correctly.", 3);
        return set;
    }

    [Fact]
    public void Cannot_activate_an_empty_set()
    {
        var set = StandardSet.Define(AccreditationFramework.JCI, "JCI", "7th");
        var act = set.Activate;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("STD-016");
    }

    [Fact]
    public void Activate_transitions_to_active_and_raises_event()
    {
        var set = DraftWithElement(out _);
        set.Activate();

        set.Status.Should().Be(StandardSetStatus.Active);
        set.DomainEvents.OfType<StandardSetActivated>().Should().ContainSingle();
    }

    [Fact]
    public void Elements_cannot_be_added_after_activation()
    {
        var set = DraftWithElement(out _);
        set.Activate();

        var act = () => set.AddElement("PC", "Patient-Centred Care", "PC.1", "PC.1.2", "text", 1);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("STD-010");
    }

    [Fact]
    public void Duplicate_element_codes_are_rejected()
    {
        var set = DraftWithElement(out _);
        var act = () => set.AddElement("PC", "Patient-Centred Care", "PC.1", "PC.1.1", "dup", 1);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("STD-014");
    }

    [Fact]
    public void Only_an_active_set_can_be_assessed()
    {
        var set = DraftWithElement(out var elementId);
        var act = () => set.AssessElement(elementId, ComplianceStatus.Compliant, null, Actor, Now);
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("STD-018");
    }

    [Fact]
    public void Assessing_an_element_records_the_verdict_and_raises_event()
    {
        var set = DraftWithElement(out var elementId);
        set.Activate();

        set.AssessElement(elementId, ComplianceStatus.PartiallyCompliant, "Bundle not fully rolled out", Actor, Now);

        var element = set.Elements.Single();
        element.ComplianceStatus.Should().Be(ComplianceStatus.PartiallyCompliant);
        element.AssessedBy.Should().Be(Actor);
        set.DomainEvents.OfType<ElementAssessed>().Should().ContainSingle();
    }
}
