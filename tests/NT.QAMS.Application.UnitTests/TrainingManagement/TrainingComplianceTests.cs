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
        session.Hold();
        session.RecordAttendance(t1, attended: true, preScore: 50, postScore: 90, passMark: course.PassMark); // pass, gain 40
        session.RecordAttendance(t2, attended: true, preScore: 60, postScore: 70, passMark: course.PassMark); // fail, gain 10
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

        var compliance = await new GetTrainingComplianceHandler(db).Handle(new GetTrainingComplianceQuery(), CancellationToken.None);
        var row = compliance.Single();
        row.CourseRef.Should().Be("CRS-2026-0001");
        row.SessionsHeld.Should().Be(1);
        row.DistinctTrainees.Should().Be(2);
        row.PassedTrainees.Should().Be(1);
        row.PassRate.Should().Be(50m);
        row.MeanPostScore.Should().Be(80m);
    }
}
