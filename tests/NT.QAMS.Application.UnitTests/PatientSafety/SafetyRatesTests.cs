using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.PatientSafety;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Domain.PatientSafety;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.PatientSafety;

/// <summary>
/// The M24→M08 loop: falls and pressure-injury rates are computed per 1,000 patient-days,
/// with the denominator taken from the ADT-derived patient-stay projection.
/// </summary>
public class SafetyRatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static AppDbContext NewContext()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"safety-rates-{Guid.NewGuid()}")
                .AddInterceptors(new TenantStampInterceptor(tenant))
                .Options, tenant);
        return db;
    }

    [Fact]
    public async Task Rates_are_per_1000_patient_days_from_the_adt_denominator()
    {
        var db = NewContext();

        // Two stays admitted 50 days before "now" → clipped to the 30-day window = 30 days each = 60 patient-days.
        var s1 = PatientStay.Admit("PT-1", "ENC-1", "Ward A", null, Now.AddDays(-50));
        var s2 = PatientStay.Admit("PT-2", "ENC-2", "ICU", null, Now.AddDays(-50));
        s1.TenantId = TenantId; s2.TenantId = TenantId;
        db.PatientStays.AddRange(s1, s2);

        // Events within the window: 3 falls, 2 pressure injuries (1 hospital-acquired).
        void AddFall(string r) { var e = PatientSafetyEvent.ReportFall(r, "PT", "Ward A", Now.AddDays(-5), HarmLevel.Minor, "x"); e.TenantId = TenantId; db.PatientSafetyEvents.Add(e); }
        AddFall("PSE-1"); AddFall("PSE-2"); AddFall("PSE-3");
        var pi1 = PatientSafetyEvent.ReportPressureInjury("PSE-4", "PT", "ICU", Now.AddDays(-3), HarmLevel.Moderate, "x", PressureInjuryStage.Stage3, InjuryOrigin.HospitalAcquired);
        var pi2 = PatientSafetyEvent.ReportPressureInjury("PSE-5", "PT", "ICU", Now.AddDays(-2), HarmLevel.Minor, "x", PressureInjuryStage.Stage2, InjuryOrigin.PresentOnAdmission);
        pi1.TenantId = TenantId; pi2.TenantId = TenantId;
        db.PatientSafetyEvents.AddRange(pi1, pi2);
        await db.SaveChangesAsync();

        var result = await new GetSafetyRatesHandler(db, new FixedClock(Now))
            .Handle(new GetSafetyRatesQuery(WindowDays: 30), CancellationToken.None);

        result.PatientDays.Should().Be(60);
        result.Falls.EventCount.Should().Be(3);
        result.Falls.RatePer1000.Should().Be(50m, "3 falls / 60 patient-days x 1000 = 50");
        result.PressureInjuries.EventCount.Should().Be(2);
        result.HospitalAcquiredPressureInjuries.Should().Be(1);
        result.HapiRatePer1000.Should().Be(decimal.Round(1000m / 60m, 2));
    }
}
