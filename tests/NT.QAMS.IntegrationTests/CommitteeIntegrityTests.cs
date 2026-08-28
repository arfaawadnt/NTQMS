using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Audit finding M-16 against a REAL PostgreSQL server: quorum counts and
/// membership are only trustworthy if the database itself refuses duplicate
/// attendance and duplicate membership rows — the aggregate guards are
/// first-line, but concurrent requests bypass in-memory checks.
/// </summary>
[Collection("real-postgres")]
public sealed class CommitteeIntegrityTests(RealPostgresFixture fx)
{
    private const string UniqueViolation = "23505";
    private static readonly DateTimeOffset At = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Duplicate_attendance_and_membership_rows_are_rejected_by_the_database()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();
        var tenant = Guid.CreateVersion7();

        var committee = NT.QAMS.Domain.Committees.Committee.Create(
            "Integrity probe committee", "terms", NT.QAMS.Domain.Committees.CommitteeFrequency.Monthly, 1);
        ((ITenantScoped)committee).TenantId = tenant;
        var memberId = Guid.CreateVersion7();
        committee.AddMember(memberId, "Chair");
        db.Committees.Add(committee);
        await db.SaveChangesAsync();

        var meeting = NT.QAMS.Domain.Committees.Meeting.Schedule(committee.Id, "MTG-UX-1", At);
        ((ITenantScoped)meeting).TenantId = tenant;
        meeting.RecordAttendance(memberId, present: true);
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        // A concurrent duplicate that slipped past the aggregate must die at
        // the unique index — a doubled row would double-count the quorum.
        await db.Database.ExecuteSqlRawAsync("SAVEPOINT probe");
        var duplicateAttendance = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO qams.meeting_attendance (id, tenant_id, user_id, present, meeting_id) "
            + "SELECT gen_random_uuid(), tenant_id, user_id, present, meeting_id "
            + "FROM qams.meeting_attendance WHERE meeting_id = {0}", meeting.Id);
        (await Assert.ThrowsAsync<PostgresException>(duplicateAttendance))
            .SqlState.Should().Be(UniqueViolation, "one attendance row per attendee per meeting");
        await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT probe");

        var duplicateMember = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO qams.committee_member (id, tenant_id, user_id, role_title, committee_id) "
            + "SELECT gen_random_uuid(), tenant_id, user_id, 'Duplicate', committee_id "
            + "FROM qams.committee_member WHERE committee_id = {0}", committee.Id);
        (await Assert.ThrowsAsync<PostgresException>(duplicateMember))
            .SqlState.Should().Be(UniqueViolation, "one membership row per user per committee");

        await tx.RollbackAsync();
    }
}
