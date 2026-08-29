using FluentAssertions;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Equipment;

/// <summary>
/// HQMS M14 completion: equipment downtime &amp; availability tracking, and the recall /
/// field-safety-notice register on the equipment aggregate.
/// </summary>
public class EquipmentDowntimeAndSafetyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 1);

    private static EquipmentItem Item() => EquipmentItem.Register("EQP-1", "Analyser", "SN-1", "Lab A", 365, 30);

    [Fact]
    public void Downtime_accrues_hours_and_reduces_availability()
    {
        var e = Item();
        var id = e.StartDowntime(T0, DowntimeCategory.Breakdown, "PSU failure");
        e.EndDowntime(id, T0.AddHours(6));

        e.Downtime.Single().DurationHours(T0.AddHours(6)).Should().Be(6);

        // 6 hours down over a 24-hour window → 75% available.
        e.Availability(T0, T0.AddHours(24)).Should().BeApproximately(0.75, 0.0001);
    }

    [Fact]
    public void Only_one_downtime_period_may_be_open_at_a_time()
    {
        var e = Item();
        e.StartDowntime(T0, DowntimeCategory.Breakdown, "x");
        var second = () => e.StartDowntime(T0.AddHours(1), DowntimeCategory.Other, "y");
        second.Should().Throw<DomainException>().Which.Code.Should().Be("EQP-031");
    }

    [Fact]
    public void Downtime_cannot_end_before_it_started()
    {
        var e = Item();
        var id = e.StartDowntime(T0, DowntimeCategory.AwaitingParts, "x");
        var act = () => e.EndDowntime(id, T0.AddHours(-1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("EQP-034");
    }

    [Fact]
    public void An_open_downtime_still_counts_toward_availability()
    {
        var e = Item();
        // Outage opens 12h into a 24h window and is still open at asOf → 12h down → 50% available.
        e.StartDowntime(T0.AddHours(12), DowntimeCategory.Breakdown, "ongoing");
        e.Availability(T0, T0.AddHours(24)).Should().BeApproximately(0.5, 0.0001);
    }

    [Fact]
    public void A_safety_notice_runs_open_then_actioned_then_closed()
    {
        var e = Item();
        var id = e.LogSafetyNotice(SafetyNoticeType.Recall, "FSN-2026-9", "Acme", SafetyNoticeSeverity.High, Today, Today.AddDays(7));
        var notice = e.SafetyNotices.Single();
        notice.Status.Should().Be(SafetyNoticeStatus.Open);

        var earlyClose = () => e.CloseSafetyNotice(id);
        earlyClose.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("EQP-043");

        e.ActionSafetyNotice(id, "Firmware patch applied.", Today.AddDays(2));
        notice.Status.Should().Be(SafetyNoticeStatus.Actioned);
        e.CloseSafetyNotice(id);
        notice.Status.Should().Be(SafetyNoticeStatus.Closed);
    }

    [Fact]
    public void An_open_notice_past_its_deadline_is_overdue()
    {
        var e = Item();
        e.LogSafetyNotice(SafetyNoticeType.FieldSafetyNotice, "FSN-1", "Maker", SafetyNoticeSeverity.Medium, Today, Today.AddDays(5));
        var notice = e.SafetyNotices.Single();
        notice.IsOverdue(Today.AddDays(5)).Should().BeFalse();
        notice.IsOverdue(Today.AddDays(6)).Should().BeTrue();
    }

    [Fact]
    public void A_notice_reference_is_required()
    {
        var e = Item();
        var act = () => e.LogSafetyNotice(SafetyNoticeType.HazardAlert, " ", "Maker", SafetyNoticeSeverity.Low, Today, null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("EQP-040");
    }

    [Fact]
    public void A_new_downtime_period_cannot_overlap_a_prior_one()
    {
        // N-13: overlapping periods double-count in the availability sum.
        var e = Item();
        var first = e.StartDowntime(T0, DowntimeCategory.Breakdown, "x");
        e.EndDowntime(first, T0.AddHours(6));

        var overlapping = () => e.StartDowntime(T0.AddHours(3), DowntimeCategory.Other, "y");
        overlapping.Should().Throw<DomainException>().Which.Code.Should().Be("EQP-035");

        // Contiguous (starting exactly when the prior ended) is allowed.
        var ok = () => e.StartDowntime(T0.AddHours(6), DowntimeCategory.Other, "z");
        ok.Should().NotThrow();
    }

    [Fact]
    public void A_safety_notice_cannot_be_actioned_before_it_was_received()
    {
        // N-13: temporal-order guard.
        var e = Item();
        var id = e.LogSafetyNotice(SafetyNoticeType.Recall, "FSN-1", "Acme", SafetyNoticeSeverity.High, Today, Today.AddDays(7));
        var act = () => e.ActionSafetyNotice(id, "patched", Today.AddDays(-1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("EQP-SN-010");
    }
}
