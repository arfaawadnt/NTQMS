using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.TrainingManagement;
using NT.QAMS.Domain.TrainingManagement;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.TrainingManagement;

/// <summary>
/// The M12 delivery loop: a course, a held session with recorded pre/post scores, then the
/// effectiveness roll-up and the compliance dashboard computed over the attendance.
/// </summary>
public class TrainingComplianceTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static AppDbContext NewContext()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"training-{Guid.NewGuid()}")
                .AddInterceptors(new TenantStampInterceptor(tenant))
                .Options, tenant);
    }

    [Fact]
    public async Task Effectiveness_and_compliance_roll_up_the_attendance()
    {
        var db = NewContext();
        var t1 = Guid.CreateVersion7();
        var t2 = Guid.CreateVersion7();

        var course = TrainingCourse.Define("CRS-2026-0001", "Fire Safety", TrainingCategory.Safety, "d", 2m, 12, 80);
        course.Activate();
        course.TenantId = TenantId;
        db.TrainingCourses.Add(course);

        var session = TrainingSession.Schedule(course.Id, "SES-2026-0001", new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), "Hall", "Trainer");
        session.RegisterAttendee(t1);
        session.RegisterAttendee(t2);
        session.Hold(course.PassMark);
        session.RecordAttendance(t1, attended: true, preScore: 50, postScore: 90); // pass, gain 40
        session.RecordAttendance(t2, attended: true, preScore: 60, postScore: 70); // fail, gain 10
        session.Close();
        session.TenantId = TenantId;
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync();

        var detail = await new GetCourseByIdHandler(db).Handle(new GetCourseByIdQuery(course.Id), CancellationToken.None);
        detail.Effectiveness.SessionsHeld.Should().Be(1);
        detail.Effectiveness.AttendedCount.Should().Be(2);
        detail.Effectiveness.PassedCount.Should().Be(1);
        detail.Effectiveness.PassRate.Should().Be(50m, "1 of 2 attendees passed");
        detail.Effectiveness.MeanPreScore.Should().Be(55m);
        detail.Effectiveness.MeanPostScore.Should().Be(80m);
        detail.Effectiveness.MeanGain.Should().Be(25m, "(40 + 10) / 2");

        var asOf = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var compliance = await new GetTrainingComplianceHandler(db, new FixedClock(asOf))
            .Handle(new GetTrainingComplianceQuery(), CancellationToken.None);
        var row = compliance.Single();
        row.CourseRef.Should().Be("CRS-2026-0001");
        row.SessionsHeld.Should().Be(1);
        row.DistinctTrainees.Should().Be(2);
        row.PassedTrainees.Should().Be(1);
        row.PassRate.Should().Be(50m);
        row.MeanPostScore.Should().Be(80m);

        // M-20: currency — the pass from 2026-09-01 with 12-month validity is
        // current one month later.
        row.CurrentTrainees.Should().Be(1);
        row.LapsedTrainees.Should().Be(0);
    }

    [Fact]
    public async Task A_pass_older_than_the_validity_window_counts_as_lapsed()
    {
        // M-20: the compliance dashboard's stated basis is CURRENCY, not
        // effectiveness alone — a stale BLS pass is a lapse, not compliance.
        var db = NewContext();
        var trainee = Guid.CreateVersion7();

        var course = TrainingCourse.Define("CRS-2026-0004", "BLS refresher", TrainingCategory.Clinical, "d", 4m, 12, 70);
        course.Activate();
        course.TenantId = TenantId;
        db.TrainingCourses.Add(course);

        var session = TrainingSession.Schedule(
            course.Id, "SES-2026-0009", new DateTimeOffset(2025, 6, 1, 9, 0, 0, TimeSpan.Zero), "Hall", "Trainer");
        session.RegisterAttendee(trainee);
        session.Hold(course.PassMark);
        session.RecordAttendance(trainee, attended: true, preScore: 60, postScore: 90);
        session.Close();
        session.TenantId = TenantId;
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync();

        var compliance = await new GetTrainingComplianceHandler(
                db, new FixedClock(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)))
            .Handle(new GetTrainingComplianceQuery(), CancellationToken.None);

        var row = compliance.Single();
        row.PassedTrainees.Should().Be(1);
        row.CurrentTrainees.Should().Be(0, "the 2025-06 pass expired 12 months later");
        row.LapsedTrainees.Should().Be(1);
    }

    [Fact]
    public async Task Sessions_cannot_be_scheduled_for_a_draft_or_retired_course()
    {
        // M-20: a Draft course is still editable and a Retired one is history —
        // neither is deliverable.
        var db = NewContext();
        var draft = TrainingCourse.Define("CRS-2026-0002", "Draft course", TrainingCategory.Safety, "d", 1m, null, 70);
        draft.TenantId = TenantId;
        db.TrainingCourses.Add(draft);
        await db.SaveChangesAsync();

        var handler = new ScheduleSessionHandler(
            db, new FakeCurrentTenant { TenantId = TenantId }, new FakeRefGenerator());
        var act = () => handler.Handle(new ScheduleSessionCommand(
            draft.Id, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), "Hall", "Trainer"), CancellationToken.None);

        (await act.Should().ThrowAsync<NT.QAMS.SharedKernel.Primitives.DomainException>())
            .Which.Code.Should().Be("CRS-013");
    }

    [Fact]
    public async Task The_pass_mark_is_snapshotted_at_hold_and_judges_every_recording()
    {
        // M-20: the live pass mark was re-read per recording, so a course edit
        // between two recordings judged attendees of ONE session against
        // DIFFERENT thresholds. The threshold is now frozen onto the session at
        // Hold and every recording is judged by that snapshot — the recording
        // handler no longer reads the course at all.
        var db = NewContext();
        var t1 = Guid.CreateVersion7();

        var course = TrainingCourse.Define("CRS-2026-0003", "BLS", TrainingCategory.Clinical, "d", 4m, 24, 70);
        course.Activate();
        course.TenantId = TenantId;
        db.TrainingCourses.Add(course);
        await db.SaveChangesAsync();

        var scheduler = new ScheduleSessionHandler(
            db, new FakeCurrentTenant { TenantId = TenantId }, new FakeRefGenerator());
        var sessionId = await scheduler.Handle(new ScheduleSessionCommand(
            course.Id, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), "Hall", "Trainer"), CancellationToken.None);

        var session = await db.TrainingSessions.SingleAsync(s => s.Id == sessionId);
        session.RegisterAttendee(t1);
        await db.SaveChangesAsync();

        await new HoldSessionHandler(db).Handle(new HoldSessionCommand(sessionId), CancellationToken.None);
        (await db.TrainingSessions.SingleAsync(s => s.Id == sessionId)).PassMarkAtHold
            .Should().Be(70, "the delivery threshold is frozen at hold");

        await new RecordAttendanceHandler(db).Handle(
            new RecordAttendanceCommand(sessionId, t1, true, 50, 75), CancellationToken.None);

        var attendance = (await db.TrainingSessions.Include(s => s.Attendance).SingleAsync(s => s.Id == sessionId)).Attendance;
        attendance.Single(a => a.TraineeId == t1).Passed.Should().BeTrue("75 >= the held mark (70)");

        // The pure-domain proof that the judgement uses the SNAPSHOT: a session
        // held at 70 passes a 75 even when the course's live mark says 80.
        var strictCourse = TrainingCourse.Define("CRS-2026-0005", "ACLS", TrainingCategory.Clinical, "d", 4m, 24, 80);
        var direct = TrainingSession.Schedule(
            strictCourse.Id, "SES-2026-0010", new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero), "Hall", "Trainer");
        var t2 = Guid.CreateVersion7();
        direct.RegisterAttendee(t2);
        direct.Hold(70); // the mark the delivery actually ran at
        direct.RecordAttendance(t2, attended: true, preScore: 50, postScore: 75);
        direct.Attendance.Single().Passed.Should().BeTrue(
            "the session judges by its held threshold, whatever the course says now");
    }
}
