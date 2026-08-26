using FluentAssertions;
using NT.QAMS.Domain.InfectionControl;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.InfectionControl;

public class DeviceExposureTests
{
    private static readonly DateTimeOffset Inserted = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_device_in_place_on_the_insertion_day_counts_as_one_device_day()
    {
        var d = DeviceExposure.Record("PT-1", "ICU", DeviceType.CentralLine, Inserted);
        d.Status.Should().Be(DeviceStatus.InPlace);
        d.DeviceDays(Inserted.AddHours(6)).Should().Be(1);
    }

    [Fact]
    public void Device_days_accrue_to_removal_then_stop()
    {
        var d = DeviceExposure.Record("PT-2", "ICU", DeviceType.Ventilator, Inserted);
        d.Remove(Inserted.AddDays(5));
        d.Status.Should().Be(DeviceStatus.Removed);
        d.RemovedAtUtc.Should().Be(Inserted.AddDays(5));

        // Ten days later the count is still fixed at the five days it was in place.
        d.DeviceDays(Inserted.AddDays(15)).Should().Be(5);
    }

    [Fact]
    public void Removal_cannot_precede_insertion()
    {
        var d = DeviceExposure.Record("PT-3", "Ward A", DeviceType.UrinaryCatheter, Inserted);
        var act = () => d.Remove(Inserted.AddDays(-1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("DEV-010");
    }

    [Fact]
    public void Removal_is_idempotent()
    {
        var d = DeviceExposure.Record("PT-4", "ICU", DeviceType.CentralLine, Inserted);
        d.Remove(Inserted.AddDays(3));
        d.Remove(Inserted.AddDays(9)); // second event ignored
        d.RemovedAtUtc.Should().Be(Inserted.AddDays(3));
    }

    [Fact]
    public void A_patient_reference_is_required()
    {
        var act = () => DeviceExposure.Record(" ", "ICU", DeviceType.CentralLine, Inserted);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("DEV-001");
    }
}
