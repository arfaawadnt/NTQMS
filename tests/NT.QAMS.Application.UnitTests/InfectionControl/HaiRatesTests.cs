using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.InfectionControl;
using NT.QAMS.Domain.InfectionControl;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.InfectionControl;

/// <summary>
/// The M09 rate engine: device-associated infection rates are per 1,000 device-days (from this
/// module's device-exposure register), while the device-utilisation ratio reuses the M24 ADT
/// patient-days as its denominator — so a single query joins both projections.
/// </summary>
public class HaiRatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static AppDbContext NewContext()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"hai-rates-{Guid.NewGuid()}")
                .AddInterceptors(new TenantStampInterceptor(tenant))
                .Options, tenant);
        return db;
    }

    [Fact]
    public async Task Clabsi_rate_is_per_1000_central_line_days_with_a_utilisation_ratio()
    {
        var db = NewContext();

        // Denominator A — patient-days: two stays admitted 50 days ago, clipped to a 30-day
        // window = 30 each = 60 patient-days.
        foreach (var (p, e, u) in new[] { ("PT-1", "ENC-1", "ICU"), ("PT-2", "ENC-2", "ICU") })
        {
            var s = PatientStay.Admit(p, e, u, null, Now.AddDays(-50));
            s.TenantId = TenantId;
            db.PatientStays.Add(s);
        }

        // Denominator B — central-line-days: two lines inserted 20 days ago, still in place =
        // 20 each = 40 central-line-days. One ventilator line inserted 10 days ago = 10 vent-days.
        void AddDevice(DeviceType t, int daysAgo)
        {
            var d = DeviceExposure.Record("PT", "ICU", t, Now.AddDays(-daysAgo));
            d.TenantId = TenantId;
            db.DeviceExposures.Add(d);
        }

        AddDevice(DeviceType.CentralLine, 20);
        AddDevice(DeviceType.CentralLine, 20);
        AddDevice(DeviceType.Ventilator, 10);

        // Numerator: 2 CLABSI, 1 VAP, 1 SSI within the window.
        void AddCase(HaiType t) { var c = HaiCase.Report("HAI", t, "PT", "ICU", Now.AddDays(-5), null, "x"); c.TenantId = TenantId; db.HaiCases.Add(c); }
        AddCase(HaiType.Clabsi);
        AddCase(HaiType.Clabsi);
        AddCase(HaiType.Vap);
        AddCase(HaiType.Ssi);
        await db.SaveChangesAsync();

        var result = await new GetHaiRatesHandler(db, new FixedClock(Now))
            .Handle(new GetHaiRatesQuery(WindowDays: 30), CancellationToken.None);

        result.PatientDays.Should().Be(60);

        result.Clabsi.DeviceDays.Should().Be(40);
        result.Clabsi.CaseCount.Should().Be(2);
        result.Clabsi.RatePer1000.Should().Be(50m, "2 CLABSI / 40 line-days x 1000 = 50");
        result.Clabsi.UtilizationRatio.Should().Be(decimal.Round(40m / 60m, 2), "central-line utilisation = line-days / patient-days");

        result.Vap.DeviceDays.Should().Be(10);
        result.Vap.CaseCount.Should().Be(1);
        result.Vap.RatePer1000.Should().Be(100m, "1 VAP / 10 vent-days x 1000 = 100");

        result.Cauti.CaseCount.Should().Be(0);
        result.Cauti.RatePer1000.Should().Be(0m, "no catheter-days and no cases yields a zero rate, not a divide-by-zero");

        result.SsiCount.Should().Be(1);
    }
}
