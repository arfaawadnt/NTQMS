using FluentAssertions;
using NT.QAMS.Domain.InfectionControl;
using NT.QAMS.Domain.Integration;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Integration;

/// <summary>
/// Audit finding M-03: the patient-/device-day accrual is the denominator of every
/// per-1,000 rate, so it must have exactly one implementation and one semantics.
/// These tests pin the canonical rules: accrual never counts past <c>asOf</c>
/// (a future-dated discharge must not inflate today's denominator), windows clip
/// on both edges, and a same-day overlap counts as one day.
/// </summary>
public class WindowAccrualTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

    private static PatientStay Stay(DateTimeOffset admitted, DateTimeOffset? discharged = null)
    {
        var stay = PatientStay.Admit("P-001", "E-001", "ICU", null, admitted);
        stay.TenantId = Tenant;
        if (discharged is { } at) { stay.Discharge(at); }
        return stay;
    }

    [Fact]
    public void Patient_days_never_count_beyond_asOf()
    {
        var stay = Stay(T0, T0.AddDays(10));

        stay.PatientDays(T0.AddDays(3)).Should().Be(3,
            "the doc promises 'or discharge, if earlier' — days beyond asOf have not happened yet");
    }

    [Fact]
    public void Device_days_never_count_beyond_asOf()
    {
        var device = DeviceExposure.Record("P-001", "ICU", DeviceType.CentralLine, T0);
        device.TenantId = Tenant;
        device.Remove(T0.AddDays(10));

        device.DeviceDays(T0.AddDays(3)).Should().Be(3);
    }

    [Fact]
    public void A_window_clips_both_edges()
    {
        var stay = Stay(T0, T0.AddDays(20));

        stay.PatientDaysInWindow(T0.AddDays(5), T0.AddDays(12)).Should().Be(7);
    }

    [Fact]
    public void A_stay_discharged_before_the_window_contributes_nothing()
    {
        var stay = Stay(T0, T0.AddDays(2));

        stay.PatientDaysInWindow(T0.AddDays(5), T0.AddDays(12)).Should().Be(0);
    }

    [Fact]
    public void A_same_day_overlap_counts_as_one_day()
    {
        var stay = Stay(T0.AddDays(6).AddHours(1), T0.AddDays(6).AddHours(9));

        stay.PatientDaysInWindow(T0.AddDays(5), T0.AddDays(12)).Should().Be(1);
    }

    [Fact]
    public void An_open_stay_accrues_only_to_asOf()
    {
        var stay = Stay(T0);

        stay.PatientDaysInWindow(T0, T0.AddDays(4)).Should().Be(4);
    }

    [Fact]
    public void A_future_dated_discharge_does_not_inflate_the_window()
    {
        var now = T0.AddDays(10);
        var stay = Stay(T0, now.AddDays(5));

        stay.PatientDaysInWindow(T0, now).Should().Be(10,
            "a discharge recorded in the future must not add days that have not elapsed");
    }
}
