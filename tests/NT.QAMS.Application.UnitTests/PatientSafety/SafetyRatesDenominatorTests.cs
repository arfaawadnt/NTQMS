using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.PatientSafety;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace NT.QAMS.Application.UnitTests.PatientSafety;

/// <summary>
/// Audit finding M-03 (handler half): the safety-rate denominator must use the
/// same clamped accrual as every other module. Before the fix this handler had
/// no <c>end &gt; now</c> clamp, so one future-dated discharge made M08 publish a
/// different patient-day denominator than M09/M10 for the identical window.
/// </summary>
public class SafetyRatesDenominatorTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_future_dated_discharge_does_not_inflate_the_denominator()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"psf-rates-{Guid.NewGuid()}")
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options, tenant);
        var stay = PatientStay.Admit("P-001", "E-001", "ICU", null, Now.AddDays(-10));
        stay.TenantId = TenantId;
        stay.Discharge(Now.AddDays(5)); // HIS clock skew / planned-discharge message
        db.PatientStays.Add(stay);
        await db.SaveChangesAsync();

        var rates = await new GetSafetyRatesHandler(db, new FixedClock(Now))
            .Handle(new GetSafetyRatesQuery(30), CancellationToken.None);

        rates.PatientDays.Should().Be(10,
            "days that have not elapsed must not enter the rate denominator");
    }

    [Fact]
    public async Task Rates_without_a_denominator_are_null_not_a_fabricated_zero()
    {
        // M-18: a window with no patient-days rendered 0.00 — indistinguishable
        // from a genuinely zero event rate. No denominator means no rate.
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"psf-rates-{Guid.NewGuid()}")
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options, tenant);

        var rates = await new GetSafetyRatesHandler(db, new FixedClock(Now))
            .Handle(new GetSafetyRatesQuery(30), CancellationToken.None);

        rates.PatientDays.Should().Be(0);
        ((decimal?)rates.Falls.RatePer1000).Should().BeNull();
        ((decimal?)rates.PressureInjuries.RatePer1000).Should().BeNull();
        ((decimal?)rates.HapiRatePer1000).Should().BeNull();
    }
}
