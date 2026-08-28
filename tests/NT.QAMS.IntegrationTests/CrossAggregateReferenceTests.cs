using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.SharedKernel.MultiTenancy;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Audit finding M-08 against a REAL PostgreSQL server: the HQMS train gave
/// every OWNED child a tenant-composite foreign key but left its
/// CROSS-AGGREGATE references bare columns — a meeting under another tenant's
/// committee, a survey response pointing at a deleted survey, or an evidence
/// link to a nonexistent element were all representable. These probes pin the
/// second line of defense: each dangling reference dies with 23503.
/// </summary>
[Collection("real-postgres")]
public sealed class CrossAggregateReferenceTests(RealPostgresFixture fx)
{
    private const string FkViolation = "23503";
    private static readonly DateTimeOffset At = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Dangling_cross_aggregate_references_are_rejected_by_the_database()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();
        var tenant = Guid.CreateVersion7();

        // Valid graphs, created the only sanctioned way — through the aggregates.
        // Parents first, in their own save: EF does not know these SQL-declared
        // FKs, so a single batch would order the inserts arbitrarily. Production
        // flows always persist the parent in an earlier command.
        var committee = NT.QAMS.Domain.Committees.Committee.Create(
            "FK probe committee", "terms", NT.QAMS.Domain.Committees.CommitteeFrequency.Monthly, 1);
        ((ITenantScoped)committee).TenantId = tenant;
        db.Committees.Add(committee);

        var survey = NT.QAMS.Domain.PatientExperience.SatisfactionSurvey.Create("FK probe survey", null);
        ((ITenantScoped)survey).TenantId = tenant;
        var questionId = survey.AddQuestion("How was the stay?", "overall");
        db.SatisfactionSurveys.Add(survey);

        var set = NT.QAMS.Domain.Accreditation.StandardSet.Define(
            NT.QAMS.Domain.Accreditation.AccreditationFramework.Other, "FK probe set", "v1");
        ((ITenantScoped)set).TenantId = tenant;
        var elementId = set.AddElement("CH1", "Chapter", "STD1", "EL1", "text", 1);
        db.StandardSets.Add(set);

        var program = NT.QAMS.Domain.AuditManagement.AuditProgram.Create(2026, "FK probe program");
        ((ITenantScoped)program).TenantId = tenant;
        var plannedId = program.AddPlannedAudit(
            "lab", null, null, NT.QAMS.Domain.AuditManagement.PlannedAuditPriority.Low, 1);
        db.AuditPrograms.Add(program);

        var endpoint = NT.QAMS.Domain.Integration.IntegrationEndpoint.Register(
            "ADT probe", NT.QAMS.Domain.Integration.InterfaceSystem.His,
            NT.QAMS.Domain.Integration.InterfaceProtocol.Hl7V2);
        ((ITenantScoped)endpoint).TenantId = tenant;
        db.IntegrationEndpoints.Add(endpoint);

        await db.SaveChangesAsync();

        var meeting = NT.QAMS.Domain.Committees.Meeting.Schedule(committee.Id, "MTG-FK-1", At);
        ((ITenantScoped)meeting).TenantId = tenant;
        db.Meetings.Add(meeting);

        var response = NT.QAMS.Domain.PatientExperience.SurveyResponse.Submit(
            survey.Id, null, null, [(questionId, 4)], At);
        ((ITenantScoped)response).TenantId = tenant;
        db.SurveyResponses.Add(response);

        var link = NT.QAMS.Domain.Accreditation.EvidenceLink.Create(
            set.Id, elementId, NT.QAMS.Domain.Accreditation.EvidenceSourceType.Other, Guid.Empty,
            "EXT-1", null, Guid.CreateVersion7(), At);
        ((ITenantScoped)link).TenantId = tenant;
        db.EvidenceLinks.Add(link);

        var message = NT.QAMS.Domain.Integration.IntegrationMessage.Receive(
            endpoint.Id, "FK-PROBE-1", "A01", "MSH|^~\\&|probe", At);
        ((ITenantScoped)message).TenantId = tenant;
        db.IntegrationMessages.Add(message);

        await db.SaveChangesAsync();

        // M-08: every cross-aggregate reference must be a tenant-composite FK.
        // The survey tables are immutable from capture (M-05), so their probes
        // are dangling INSERTs — the UPDATE path is already dead at the
        // immutability trigger before the FK could speak.
        var probes = new (string Name, string Sql, Guid Id)[]
        {
            ("meeting → committee", "UPDATE qams.meeting SET committee_id = gen_random_uuid() WHERE id = {0}", meeting.Id),
            ("response → survey",
                "INSERT INTO qams.survey_response (id, tenant_id, survey_id, submitted_at_utc, created_at_utc) "
                + "SELECT gen_random_uuid(), tenant_id, gen_random_uuid(), now(), now() FROM qams.survey_response WHERE id = {0}", response.Id),
            ("response → department",
                "INSERT INTO qams.survey_response (id, tenant_id, survey_id, department_id, submitted_at_utc, created_at_utc) "
                + "SELECT gen_random_uuid(), tenant_id, survey_id, gen_random_uuid(), now(), now() FROM qams.survey_response WHERE id = {0}", response.Id),
            ("answer → question",
                "INSERT INTO qams.survey_answer (id, tenant_id, question_id, score, survey_response_id) "
                + "SELECT gen_random_uuid(), tenant_id, gen_random_uuid(), 3, id FROM qams.survey_response WHERE id = {0}", response.Id),
            ("evidence → standard set", "UPDATE qams.evidence_link SET standard_set_id = gen_random_uuid() WHERE id = {0}", link.Id),
            ("evidence → element", "UPDATE qams.evidence_link SET element_id = gen_random_uuid() WHERE id = {0}", link.Id),
            ("planned audit → audit", "UPDATE qams.planned_audit SET scheduled_audit_id = gen_random_uuid() WHERE id = {0}", plannedId),
            ("message → endpoint", "UPDATE qams.integration_message SET endpoint_id = gen_random_uuid() WHERE id = {0}", message.Id),
        };

        foreach (var (name, sql, id) in probes)
        {
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT probe");
            var attack = () => db.Database.ExecuteSqlRawAsync(sql, id);
            (await Assert.ThrowsAsync<PostgresException>(attack))
                .SqlState.Should().Be(FkViolation, $"'{name}' must be a tenant-composite foreign key");
            await db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT probe");
        }

        await tx.RollbackAsync();
    }
}
