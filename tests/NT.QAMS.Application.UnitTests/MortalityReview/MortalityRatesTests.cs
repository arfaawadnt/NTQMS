using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.MortalityReview;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Domain.MortalityReview;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.MortalityReview;

/// <summary>
/// The M24 → M10 loop: the mortality rate is per 1,000 patient-days from the ADT denominator,
/// alongside the peer-review classification breakdown and the complication counts.
/// </summary>
public class MortalityRatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid R1 = Guid.CreateVersion7();
    private static readonly Guid R2 = Guid.CreateVersion7();

    private static AppDbContext NewContext()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"mortality-rates-{Guid.NewGuid()}")
                .AddInterceptors(new TenantStampInterceptor(tenant))
                .Options, tenant);
    }

    [Fact]
    public async Task Mortality_rate_is_per_1000_patient_days_with_the_classification_breakdown()
    {
        var db = NewContext();

        // Two 50-day stays clipped to a 30-day window = 60 patient-days.
        foreach (var (p, e) in new[] { ("PT-1", "ENC-1"), ("PT-2", "ENC-2") })
        {
            var s = PatientStay.Admit(p, e, "ICU", null, Now.AddDays(-50));
            s.TenantId = TenantId;
            db.PatientStays.Add(s);
        }

        // Three deaths in-window: 1 expected, 1 unexpected, 1 potentially-preventable.
        void AddDeath(string r, DeathClassification k)
        {
            var m = Domain.MortalityReview.MortalityReview.Report(r, "PT", "ICU", Now.AddDays(-4), "dx");
            m.Classify(R1, k, "findings");
            if (k != DeathClassification.Expected) { m.RecordSecondReview(R2, "concur", true); }
            m.TenantId = TenantId;
            db.MortalityReviews.Add(m);
        }

        AddDeath("MRT-1", DeathClassification.Expected);
        AddDeath("MRT-2", DeathClassification.Unexpected);
        AddDeath("MRT-3", DeathClassification.PotentiallyPreventable);

        // Two complications, one judged preventable.
        void AddComp(string r, bool preventable)
        {
            var c = ComplicationCase.Report(r, "PT", "Ward", ComplicationType.UnplannedReadmission,
                ComplicationSeverity.Moderate, Now.AddDays(-3), "x");
            c.RecordReview(R1, "reviewed", preventable, Now.AddDays(-2));
            c.TenantId = TenantId;
            db.ComplicationCases.Add(c);
        }

        AddComp("CMP-1", preventable: true);
        AddComp("CMP-2", preventable: false);
        await db.SaveChangesAsync();

        var result = await new GetMortalityRatesHandler(db, new FixedClock(Now))
            .Handle(new GetMortalityRatesQuery(WindowDays: 30), CancellationToken.None);

        result.PatientDays.Should().Be(60);
        result.Deaths.Should().Be(3);
        result.MortalityRatePer1000.Should().Be(50m, "3 deaths / 60 patient-days x 1000 = 50");
        result.Expected.Should().Be(1);
        result.Unexpected.Should().Be(1);
        result.PotentiallyPreventable.Should().Be(1);
        result.Preventable.Should().Be(0);
        result.Complications.Should().Be(2);
        result.PreventableComplications.Should().Be(1);
    }
}
