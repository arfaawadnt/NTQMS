using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Verifies audit finding F-02 against a REAL PostgreSQL server: once a record
/// is signed off, the database itself (not just the domain) rejects any UPDATE
/// or DELETE — while still allowing the legitimate transition INTO the signed
/// state. Everything runs inside a rolled-back transaction, so the otherwise
/// un-deletable signed row never persists.
/// </summary>
[Collection("real-postgres")]
public sealed class SignedRecordImmutabilityTests(RealPostgresFixture fx)
{
    private static readonly DateTimeOffset At = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Sign_off_transition_succeeds_then_raw_update_is_rejected()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        var tenant = Guid.CreateVersion7();
        ctx.Set(tenant);
        await using var tx = await db.Database.BeginTransactionAsync();

        var signed = await SeedSignedScreeningAsync(db, tenant);

        // The trigger must NOT have blocked the transition into SignedOff.
        signed.State.Should().Be(OutlierScreeningState.SignedOff);

        var tamper = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE qams.outlier_screening SET dataset = 'TAMPERED' WHERE id = {0}", signed.Id);

        (await tamper.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("23514", "the immutability trigger raises a check_violation");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Signed_record_raw_delete_is_rejected()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        var tenant = Guid.CreateVersion7();
        ctx.Set(tenant);
        await using var tx = await db.Database.BeginTransactionAsync();

        var signed = await SeedSignedScreeningAsync(db, tenant);

        var delete = async () => await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM qams.outlier_screening WHERE id = {0}", signed.Id);

        (await delete.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("23514");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Hqms_frozen_records_reject_raw_update_and_delete()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        var tenant = Guid.CreateVersion7();
        ctx.Set(tenant);
        await using var tx = await db.Database.BeginTransactionAsync();

        // A CLOSED (signed) incident.
        var incident = NT.QAMS.Domain.IncidentReporting.Incident.Report(
            "INC-ITEST-0001", "Frozen probe", "Immutability probe incident",
            NT.QAMS.Domain.IncidentReporting.IncidentCategory.Other,
            NT.QAMS.Domain.IncidentReporting.HarmGrade.NoHarm,
            NT.QAMS.Domain.IncidentReporting.IntakeChannel.Web, At, Guid.CreateVersion7());
        incident.TenantId = tenant;
        incident.Triage(Guid.CreateVersion7(), NT.QAMS.Domain.IncidentReporting.IncidentCategory.Other);
        incident.StartInvestigation(Guid.CreateVersion7());
        incident.RecordInvestigationSummary("Probe summary.");
        incident.SubmitForReview();
        incident.Close("Probe closure.", Guid.CreateVersion7());
        db.Incidents.Add(incident);

        // Real parents first (M-08 made dangling cross-aggregate references
        // structurally impossible), in their own save so the batch order can't
        // put a child before its parent.
        var committee = NT.QAMS.Domain.Committees.Committee.Create(
            "Immutability probe committee", "terms",
            NT.QAMS.Domain.Committees.CommitteeFrequency.Monthly, 1);
        ((ITenantScoped)committee).TenantId = tenant;
        db.Committees.Add(committee);

        var survey = NT.QAMS.Domain.PatientExperience.SatisfactionSurvey.Create("Immutability probe survey", null);
        ((ITenantScoped)survey).TenantId = tenant;
        var questionId = survey.AddQuestion("How was the stay?", "overall");
        db.SatisfactionSurveys.Add(survey);
        await db.SaveChangesAsync();

        // A meeting with APPROVED minutes.
        var attendee = Guid.CreateVersion7();
        var meeting = NT.QAMS.Domain.Committees.Meeting.Schedule(committee.Id, "MTG-ITEST-0001", At);
        ((ITenantScoped)meeting).TenantId = tenant;
        meeting.RecordAttendance(attendee, present: true);
        meeting.Hold(committeeQuorum: 1);
        meeting.RecordMinutes("Probe minutes.");
        meeting.ApproveMinutes(attendee);
        db.Meetings.Add(meeting);

        // A survey response — immutable from capture.
        var response = NT.QAMS.Domain.PatientExperience.SurveyResponse.Submit(
            survey.Id, null, null, [(questionId, 4)], At);
        ((ITenantScoped)response).TenantId = tenant;
        db.SurveyResponses.Add(response);

        await db.SaveChangesAsync();

        // M-05: all three HQMS frozen-record types are database-immutable.
        var tampers = new (string Name, string Sql, object Id)[]
        {
            ("closed incident update", "UPDATE qams.incident SET title = 'TAMPERED' WHERE id = {0}", incident.Id),
            ("closed incident delete", "DELETE FROM qams.incident WHERE id = {0}", incident.Id),
            ("approved minutes update", "UPDATE qams.meeting SET minutes = 'TAMPERED' WHERE id = {0}", meeting.Id),
            ("approved minutes delete", "DELETE FROM qams.meeting WHERE id = {0}", meeting.Id),
            ("survey response update", "UPDATE qams.survey_response SET service_line = 'TAMPERED' WHERE id = {0}", response.Id),
            ("survey response delete", "DELETE FROM qams.survey_response WHERE id = {0}", response.Id),
            ("survey answer update", "UPDATE qams.survey_answer SET score = 1 WHERE survey_response_id = {0}", response.Id),
        };

        foreach (var (name, sql, id) in tampers)
        {
            // Savepoint per probe: a rejected statement aborts the enclosing
            // transaction, and the next probe would otherwise die with 25P02.
            await tx.CreateSavepointAsync("tamper");
            var tamper = async () => await db.Database.ExecuteSqlRawAsync(sql, id);
            (await tamper.Should().ThrowAsync<PostgresException>($"'{name}' must be rejected"))
                .Which.SqlState.Should().Be("23514", $"'{name}' trips the immutability trigger");
            await tx.RollbackToSavepointAsync("tamper");
        }

        await tx.RollbackAsync();
    }

    /// <summary>Create → enter points → calculate → sign off, all persisted in the current transaction.</summary>
    private static async Task<OutlierScreening> SeedSignedScreeningAsync(AppDbContext db, Guid tenant)
    {
        var s = OutlierScreening.Configure("ITEST-SIGN", "dataset", "u");
        ((ITenantScoped)s).TenantId = tenant;
        foreach (var v in new[] { 10m, 11m, 12m, 13m, 100m })
        {
            s.AddPoint(v, "p");
        }

        db.OutlierScreenings.Add(s);
        await db.SaveChangesAsync();

        s.Calculate();
        await db.SaveChangesAsync();

        s.SignOff(Guid.CreateVersion7(), At); // transition INTO SignedOff — must be allowed
        await db.SaveChangesAsync();
        return s;
    }
}
