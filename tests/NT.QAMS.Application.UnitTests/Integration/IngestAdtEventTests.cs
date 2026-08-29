using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Integration;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Integration;

/// <summary>
/// The ADT ingestion pipeline (HQMS M24): messages are idempotent by dedup key, admit builds
/// a patient-stay, and a processing error (e.g. discharge with no stay) is captured on the
/// message as Failed rather than lost.
/// </summary>
public class IngestAdtEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static AppDbContext NewContext(FakeCurrentTenant tenant) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"adt-{Guid.NewGuid()}")
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options, tenant);

    private static async Task<(AppDbContext Db, FakeCurrentTenant Tenant, Guid EndpointId)> SeedAsync()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var db = NewContext(tenant);
        var endpoint = IntegrationEndpoint.Register("HIS ADT", InterfaceSystem.His, InterfaceProtocol.Hl7V2);
        endpoint.TenantId = TenantId;
        db.IntegrationEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        return (db, tenant, endpoint.Id);
    }

    private sealed class NoViolations : NT.QAMS.Application.Abstractions.IDatabaseErrorClassifier
    {
        public bool IsUniqueViolation(Exception exception) => false;
    }

    private static IngestAdtEventHandler Handler(AppDbContext db) => new(db, new FixedClock(Now), new NoViolations());

    private static IngestAdtEventCommand Admit(Guid endpointId, string dedup, string enc) =>
        new(endpointId, dedup, "ADT^A01", "raw", "Admit", "PT-1", enc, "Ward A", null, Now);

    [Fact]
    public async Task Admit_creates_a_stay_and_marks_the_message_processed()
    {
        var (db, _, endpointId) = await SeedAsync();

        var result = await Handler(db).Handle(Admit(endpointId, "M1", "ENC-1"), CancellationToken.None);

        result.Status.Should().Be("Processed");
        (await db.PatientStays.SingleAsync()).EncounterRef.Should().Be("ENC-1");
        (await db.IntegrationMessages.SingleAsync()).Status.Should().Be(MessageStatus.Processed);
    }

    [Fact]
    public async Task Redelivery_of_the_same_dedup_key_does_not_duplicate()
    {
        var (db, _, endpointId) = await SeedAsync();
        var handler = Handler(db);

        await handler.Handle(Admit(endpointId, "M1", "ENC-1"), CancellationToken.None);
        await handler.Handle(Admit(endpointId, "M1", "ENC-1"), CancellationToken.None); // redelivery

        (await db.IntegrationMessages.CountAsync()).Should().Be(1);
        (await db.PatientStays.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Discharge_with_no_prior_stay_is_captured_as_a_failed_message()
    {
        var (db, _, endpointId) = await SeedAsync();

        var result = await Handler(db).Handle(
            new IngestAdtEventCommand(endpointId, "M9", "ADT^A03", "raw", "Discharge",
                "PT-9", "ENC-UNKNOWN", "Ward A", null, Now),
            CancellationToken.None);

        result.Status.Should().Be("Failed");
        result.Error.Should().Contain("No stay");
        (await db.IntegrationMessages.SingleAsync()).Status.Should().Be(MessageStatus.Failed);
        (await db.PatientStays.AnyAsync()).Should().BeFalse();
        // The endpoint recorded the failure.
        (await db.IntegrationEndpoints.SingleAsync()).ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public async Task Admit_then_discharge_sets_the_stay_discharged()
    {
        var (db, _, endpointId) = await SeedAsync();
        var handler = Handler(db);

        await handler.Handle(Admit(endpointId, "M1", "ENC-1"), CancellationToken.None);
        await handler.Handle(
            new IngestAdtEventCommand(endpointId, "M2", "ADT^A03", "raw", "Discharge",
                "PT-1", "ENC-1", "Ward A", null, Now.AddDays(4)),
            CancellationToken.None);

        var stay = await db.PatientStays.SingleAsync();
        stay.Status.Should().Be(StayStatus.Discharged);
        stay.PatientDays(Now.AddDays(10)).Should().Be(4);
    }

    [Fact]
    public async Task An_admit_refresh_with_a_different_patient_is_rejected_not_silently_merged()
    {
        // M-12: a repeated admit for the same encounter but ANOTHER patient is
        // corrupt feed data — silently refreshing the unit would hide it and
        // quietly misattribute the census.
        var (db, _, endpointId) = await SeedAsync();
        await Handler(db).Handle(Admit(endpointId, "M1", "ENC-1"), CancellationToken.None);

        var mismatch = new IngestAdtEventCommand(
            endpointId, "M2", "ADT^A01", "raw", "Admit", "PT-2", "ENC-1", "Ward B", null, Now);
        var result = await Handler(db).Handle(mismatch, CancellationToken.None);

        result.Status.Should().Be("Failed", "the same encounter cannot swap patients silently");
        result.Error.Should().NotBeNullOrEmpty();
        (await db.PatientStays.SingleAsync()).PatientRef.Should().Be("PT-1", "the stay keeps its original patient");
    }
}
