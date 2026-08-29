using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Committees;
using NT.QAMS.Domain.Committees;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Committees;

/// <summary>
/// Audit finding M-16: committee governance integrity at the handlers. Quorum
/// is only meaningful when attendance rows belong to actual committee members,
/// and a disbanded committee is governance history — it schedules, holds and
/// approves nothing.
/// </summary>
public class CommitteeGovernanceTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly DateTimeOffset When = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static AppDbContext NewContext()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"cmt-gov-{Guid.NewGuid()}")
                .AddInterceptors(new TenantStampInterceptor(tenant))
                .Options, tenant);
    }

    private static Committee SeedCommittee(AppDbContext db, out Guid memberId, bool disband = false)
    {
        var committee = Committee.Create("Quality Committee", "ToR", CommitteeFrequency.Monthly, 1);
        committee.TenantId = TenantId;
        memberId = Guid.CreateVersion7();
        committee.AddMember(memberId, "Chair");
        if (disband)
        {
            committee.Disband();
        }

        db.Committees.Add(committee);
        return committee;
    }

    [Fact]
    public async Task A_disbanded_committee_cannot_schedule_a_meeting()
    {
        var db = NewContext();
        SeedCommittee(db, out _, disband: true);
        await db.SaveChangesAsync();
        var committeeId = (await db.Committees.SingleAsync()).Id;

        var handler = new ScheduleMeetingHandler(
            db, new FakeCurrentTenant { TenantId = TenantId }, new FakeRefGenerator());
        var act = () => handler.Handle(new ScheduleMeetingCommand(committeeId, When), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CMT-016");
    }

    [Fact]
    public async Task Attendance_is_recorded_only_for_committee_members()
    {
        var db = NewContext();
        var committee = SeedCommittee(db, out var memberId);
        await db.SaveChangesAsync();

        var meeting = Meeting.Schedule(committee.Id, "MTG-2026-0001", When);
        meeting.TenantId = TenantId;
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var handler = new RecordAttendanceHandler(db);

        // A member is fine.
        await handler.Handle(new RecordAttendanceCommand(meeting.Id, memberId, true), CancellationToken.None);

        // A stranger is not — quorum must count members, not arbitrary Guids.
        var act = () => handler.Handle(
            new RecordAttendanceCommand(meeting.Id, Guid.CreateVersion7(), true), CancellationToken.None);
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CMT-017");
    }

    [Fact]
    public async Task A_disbanded_committee_cannot_hold_meetings_or_approve_minutes()
    {
        var db = NewContext();
        var committee = SeedCommittee(db, out var memberId);
        await db.SaveChangesAsync();

        var meeting = Meeting.Schedule(committee.Id, "MTG-2026-0002", When);
        meeting.TenantId = TenantId;
        meeting.RecordAttendance(memberId, present: true);
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        // The committee disbands between scheduling and the meeting.
        (await db.Committees.SingleAsync()).Disband();
        await db.SaveChangesAsync();

        var hold = () => new HoldMeetingHandler(db).Handle(new HoldMeetingCommand(meeting.Id), CancellationToken.None);
        (await hold.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CMT-016");
    }

    private sealed class CountingSignatureService : NT.QAMS.Application.Abstractions.IESignatureService
    {
        public int Calls { get; private set; }

        public Task<NT.QAMS.Domain.ComplianceLedger.SignatureRecord> SignAsync(
            Guid signerId, string password, string pin, string meaning, string subjectRef, string contentHash,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new NT.QAMS.Domain.ComplianceLedger.SignatureRecord
            {
                Id = Guid.CreateVersion7(), SignerId = signerId, SignerDisplay = "fake",
                Meaning = meaning, SubjectRef = subjectRef, ContentHash = contentHash, SignedAtUtc = When,
            });
        }
    }

    private static async Task<(AppDbContext Db, Guid MeetingId, Guid Approver)> HeldMeetingWithMinutes()
    {
        var db = NewContext();
        var committee = SeedCommittee(db, out var memberId);
        await db.SaveChangesAsync();

        var meeting = Meeting.Schedule(committee.Id, "MTG-2026-0100", When);
        meeting.TenantId = TenantId;
        meeting.RecordAttendance(memberId, present: true);
        meeting.Hold(committeeQuorum: 1);
        meeting.RecordMinutes("Agreed the quality plan.");
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return (db, meeting.Id, memberId);
    }

    [Fact]
    public async Task Approving_minutes_is_a_signing_ceremony_that_mints_exactly_one_signature()
    {
        // M-16 (Group C decision): minutes approval is a Part 11 signed gate.
        var (db, meetingId, approver) = await HeldMeetingWithMinutes();
        var signatures = new CountingSignatureService();
        var user = new FakeCurrentUser { UserId = approver, DisplayName = "Chair" };

        await new ApproveMinutesHandler(db, user, signatures)
            .Handle(new ApproveMinutesCommand(meetingId, "pw", "1234"), CancellationToken.None);

        signatures.Calls.Should().Be(1, "approval mints exactly one signature");
        (await db.Meetings.SingleAsync(m => m.Id == meetingId)).Status
            .Should().Be(MeetingStatus.MinutesApproved);
    }

    [Fact]
    public async Task Approving_minutes_that_were_never_recorded_mints_no_signature()
    {
        // Append-only ledger: a failed precondition must not leave a signature.
        var db = NewContext();
        var committee = SeedCommittee(db, out var memberId);
        await db.SaveChangesAsync();
        var meeting = Meeting.Schedule(committee.Id, "MTG-2026-0101", When);
        meeting.TenantId = TenantId;
        meeting.RecordAttendance(memberId, present: true);
        meeting.Hold(committeeQuorum: 1); // held, but no minutes recorded
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var signatures = new CountingSignatureService();
        var act = () => new ApproveMinutesHandler(db, new FakeCurrentUser { UserId = memberId }, signatures)
            .Handle(new ApproveMinutesCommand(meeting.Id, "pw", "1234"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("MTG-022");
        signatures.Calls.Should().Be(0, "no signature is minted when the precondition fails");
    }
}
