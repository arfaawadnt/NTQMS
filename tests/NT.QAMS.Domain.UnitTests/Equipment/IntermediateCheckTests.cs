using FluentAssertions;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Equipment;

public sealed class IntermediateCheckTests
{
    private static readonly Guid Technician = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 7, 25);

    private static EquipmentItem NewEquipment()
    {
        var item = EquipmentItem.Register("EQP-2026-0001", "Cobas c503", "SN-1", "Lab A", 365, 14);
        item.LogCalibration(Today.AddDays(-30), "Roche Service", "Pass", null);
        item.ClearDomainEvents();
        return item;
    }

    [Fact]
    public void Passing_check_is_recorded_without_raising_an_alarm()
    {
        var item = NewEquipment();
        var checkId = item.RecordIntermediateCheck(
            Today, Technician, "Zero/drift check", passed: true, referenceStandardId: Guid.CreateVersion7(), remarks: null);

        item.IntermediateChecks.Should().ContainSingle(c => c.Id == checkId && c.Passed);
        item.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Failed_check_raises_the_event_that_opens_an_nc()
    {
        var item = NewEquipment();
        item.RecordIntermediateCheck(
            Today, Technician, "Control weight check", passed: false, referenceStandardId: null, remarks: "Reads +0.4%");

        var failure = item.DomainEvents.OfType<IntermediateCheckFailed>().Single();
        failure.CheckType.Should().Be("Control weight check");
        failure.PerformedById.Should().Be(Technician);
    }

    [Fact]
    public void Checks_demand_a_type_and_refuse_retired_equipment()
    {
        var item = NewEquipment();

        var noType = () => item.RecordIntermediateCheck(Today, Technician, " ", true, null, null);
        noType.Should().Throw<DomainException>().Which.Code.Should().Be("EQP-021");

        item.Retire();
        var onRetired = () => item.RecordIntermediateCheck(Today, Technician, "Zero check", true, null, null);
        onRetired.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("EQP-020");
    }
}
