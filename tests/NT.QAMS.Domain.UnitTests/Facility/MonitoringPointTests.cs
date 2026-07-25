using FluentAssertions;
using NT.QAMS.Domain.Facility;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Facility;

public sealed class MonitoringPointTests
{
    private static readonly Guid Technician = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 8, 0, 0, TimeSpan.Zero);

    private static MonitoringPoint FridgePoint() => MonitoringPoint.Register(
        "ENV-2026-0001", "Fridge 2 — Reagent storage", "Main lab", "Temperature", "°C",
        lowLimit: 2m, highLimit: 8m);

    [Fact]
    public void Registration_demands_a_coherent_acceptance_window()
    {
        var noLimits = () => MonitoringPoint.Register("ENV-1", "X", null, "Temperature", "°C", null, null);
        noLimits.Should().Throw<DomainException>().Which.Code.Should().Be("ENV-003");

        var inverted = () => MonitoringPoint.Register("ENV-1", "X", null, "Temperature", "°C", 8m, 2m);
        inverted.Should().Throw<DomainException>().Which.Code.Should().Be("ENV-004");

        var oneSided = MonitoringPoint.Register("ENV-1", "Freezer", null, "Temperature", "°C", null, -18m);
        oneSided.HighLimit.Should().Be(-18m);
        oneSided.Status.Should().Be(MonitoringPointStatus.Active);
    }

    [Fact]
    public void Boundary_readings_are_in_limit_and_raise_nothing()
    {
        var point = FridgePoint();
        point.RecordReading(2m, Now, Technician, null);
        point.RecordReading(8m, Now, Technician, null);
        point.RecordReading(5.5m, Now, Technician, "routine");

        point.Readings.Should().HaveCount(3).And.OnlyContain(r => r.InLimit);
        point.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Excursion_raises_the_event_that_opens_an_nc()
    {
        var point = FridgePoint();
        point.RecordReading(9.4m, Now, Technician, "Door left ajar");

        point.Readings.Single().InLimit.Should().BeFalse();
        var excursion = point.DomainEvents.OfType<EnvironmentalExcursionDetected>().Single();
        excursion.Value.Should().Be(9.4m);
        excursion.HighLimit.Should().Be(8m);
        excursion.RecordedById.Should().Be(Technician);
    }

    [Fact]
    public void Rebaselining_limits_never_rewrites_recorded_verdicts()
    {
        var point = FridgePoint();
        point.RecordReading(7.5m, Now, Technician, null); // In limit under 2–8.

        point.SetLimits(2m, 6m); // Tighten after the fact.

        point.Readings.Single().InLimit.Should().BeTrue("verdicts are frozen at recording time");
        point.RecordReading(7.5m, Now.AddHours(1), Technician, null);
        point.Readings[1].InLimit.Should().BeFalse("new readings use the new window");
    }

    [Fact]
    public void Lifecycle_guards_suspended_and_retired_points()
    {
        var point = FridgePoint();
        point.Suspend();

        var onSuspended = () => point.RecordReading(5m, Now, Technician, null);
        onSuspended.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("ENV-011");

        point.Resume();
        point.RecordReading(5m, Now, Technician, null);

        point.Retire();
        var rebaseline = () => point.SetLimits(1m, 9m);
        rebaseline.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("ENV-010");
    }
}
