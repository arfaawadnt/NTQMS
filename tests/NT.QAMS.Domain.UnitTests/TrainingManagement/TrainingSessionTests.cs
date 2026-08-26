using FluentAssertions;
using NT.QAMS.Domain.TrainingManagement;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.TrainingManagement;

public class TrainingSessionTests
{
    private static readonly Guid CourseId = Guid.CreateVersion7();
    private static readonly Guid Trainee = Guid.CreateVersion7();
    private static readonly DateTimeOffset When = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static TrainingSession Scheduled() =>
        TrainingSession.Schedule(CourseId, "SES-1", When, "Room A", "Dr Smith");

    [Fact]
    public void Attendance_records_the_pre_post_gain_and_pass()
    {
        var s = Scheduled();
        s.RegisterAttendee(Trainee);
        s.Hold();
        s.RecordAttendance(Trainee, attended: true, preScore: 40, postScore: 90, passMark: 80);

        var line = s.Attendance.Single();
        line.ScoreGain.Should().Be(50);
        line.Passed.Should().BeTrue();
        s.AttendedCount.Should().Be(1);
    }

    [Fact]
    public void A_post_score_below_the_pass_mark_does_not_pass()
    {
        var s = Scheduled();
        s.RegisterAttendee(Trainee);
        s.Hold();
        s.RecordAttendance(Trainee, attended: true, preScore: 40, postScore: 70, passMark: 80);
        s.Attendance.Single().Passed.Should().BeFalse();
    }

    [Fact]
    public void Absence_never_passes_even_with_a_high_post_score()
    {
        var s = Scheduled();
        s.RegisterAttendee(Trainee);
        s.Hold();
        s.RecordAttendance(Trainee, attended: false, preScore: null, postScore: 95, passMark: 80);
        s.Attendance.Single().Passed.Should().BeFalse();
    }

    [Fact]
    public void Cannot_register_the_same_trainee_twice()
    {
        var s = Scheduled();
        s.RegisterAttendee(Trainee);
        var again = () => s.RegisterAttendee(Trainee);
        again.Should().Throw<DomainException>().Which.Code.Should().Be("SES-011");
    }

    [Fact]
    public void Attendance_can_only_be_recorded_while_held()
    {
        var s = Scheduled();
        s.RegisterAttendee(Trainee);
        var early = () => s.RecordAttendance(Trainee, true, 40, 90, 80);
        early.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("SES-013");
    }

    [Fact]
    public void Recording_an_unregistered_trainee_is_rejected()
    {
        var s = Scheduled();
        s.Hold();
        var act = () => s.RecordAttendance(Trainee, true, 40, 90, 80);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SES-015");
    }

    [Fact]
    public void Scheduled_to_held_to_closed_is_the_happy_path()
    {
        var s = Scheduled();
        s.Hold();
        s.Close();
        s.Status.Should().Be(SessionStatus.Closed);

        var cancelAfterClose = s.Cancel;
        cancelAfterClose.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("SES-017");
    }
}
