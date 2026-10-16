using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.AnalyticalQuality;

/// <summary>
/// EP05/EP15 gap closure through the handlers: multi-level series, day-nested
/// replicates with measurement conditions, the workspace read (series members,
/// coverage, PT between-lab context) and the signed-off within-lab CV feeding
/// measurement uncertainty and the sigma metric.
/// </summary>
public class PrecisionWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static AppDbContext NewDb()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"aq-precision-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(new FixedClock(Now), new FakeCurrentUser()),
                new TenantStampInterceptor(tenant))
            .Options;
        return new AppDbContext(options, tenant);
    }

    private static FakeCurrentTenant Tenant() => new() { TenantId = TenantId };

    private static async Task<PrecisionStudy> SignedOffStudyAsync(AppDbContext db, string analyte = "Glucose")
    {
        var study = PrecisionStudy.Configure("PR-2026-0042", analyte, "mmol/L", "Level 1", null, null);
        study.AddMeasurement("A", 10m);
        study.AddMeasurement("A", 12m);
        study.AddMeasurement("B", 14m);
        study.AddMeasurement("B", 16m);
        study.Calculate();
        study.SignOff(Guid.CreateVersion7(), Now);
        db.PrecisionStudies.Add(study);
        await db.SaveChangesAsync();
        return study;
    }

    [Fact]
    public async Task A_series_creates_one_linked_study_per_level_with_its_own_claims()
    {
        await using var db = NewDb();
        var handler = new CreatePrecisionSeriesHandler(db, Tenant(), new FakeRefGenerator());

        var created = await handler.Handle(new CreatePrecisionSeriesCommand("Glucose", "mmol/L",
            [new("Low", 2.5m, 4m), new("High", 1.8m, null)]), CancellationToken.None);

        created.SeriesRef.Should().StartWith("PRS-");
        created.StudyIds.Should().HaveCount(2);
        var studies = await db.PrecisionStudies.AsNoTracking().OrderBy(s => s.StudyRef).ToListAsync();
        studies.Should().OnlyContain(s => s.SeriesRef == created.SeriesRef && s.SeriesLevelCount == 2);
        studies.Select(s => s.Level).Should().Equal("Low", "High");
        studies[1].ClaimedWithinLabCvPct.Should().BeNull();
    }

    [Fact]
    public void The_series_validator_requires_distinct_levels_and_a_bounded_count()
    {
        var validator = new CreatePrecisionSeriesValidator();

        validator.Validate(new CreatePrecisionSeriesCommand("Glucose", "mmol/L", [new("Low", null, null)]))
            .IsValid.Should().BeFalse();
        validator.Validate(new CreatePrecisionSeriesCommand("Glucose", "mmol/L",
            [new("Low", null, null), new(" low", null, null)])).IsValid.Should().BeFalse();
        validator.Validate(new CreatePrecisionSeriesCommand("Glucose", "mmol/L",
            [new("Low", null, null), new("High", -1m, null)])).IsValid.Should().BeFalse();
        validator.Validate(new CreatePrecisionSeriesCommand("Glucose", "mmol/L",
            [new("Low", null, null), new("High", 2m, 3m)])).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Replicates_carry_their_day_and_conditions_into_the_workspace_read()
    {
        await using var db = NewDb();
        var study = PrecisionStudy.Configure("PR-2026-0001", "Glucose", "mmol/L", "Level 1", null, null);
        db.PrecisionStudies.Add(study);
        await db.SaveChangesAsync();
        var handlers = new PrecisionWorkflowHandlers(db, new FakeCurrentUser(), new FixedClock(Now), null!);

        foreach (var (day, run, value) in new[]
                 {
                     ("D1", "R1", 10m), ("D1", "R1", 12m), ("D1", "R2", 14m), ("D1", "R2", 16m),
                     ("D2", "R1", 20m), ("D2", "R1", 22m), ("D2", "R2", 24m), ("D2", "R2", 26m),
                 })
        {
            await handlers.Handle(new AddPrecisionMeasurementCommand(
                study.Id, run, value, day, "Analyst A", "Cobas-1", day == "D1" ? "LOT-1" : "LOT-2", "CAL-9",
                new DateOnly(2026, 10, day == "D1" ? 1 : 2)), CancellationToken.None);
        }

        await handlers.Handle(new CalculatePrecisionCommand(study.Id), CancellationToken.None);
        var detail = await new GetPrecisionStudyByIdHandler(db)
            .Handle(new GetPrecisionStudyByIdQuery(study.Id), CancellationToken.None);

        detail.Design.Should().Be(nameof(PrecisionDesign.DaysAndRuns));
        detail.BetweenDaySd.Should().BeApproximately(6.7823m, 0.0005m);
        detail.Runs.Should().HaveCount(4);
        detail.Measurements.Should().OnlyContain(m => m.Instrument == "Cobas-1" && m.DayLabel != null);
        detail.Coverage.Days.Should().Be(2);
        detail.Coverage.ReagentLots.Should().Be(2);
        detail.Coverage.MeetsEp15MinimumDesign.Should().BeFalse();
        detail.SeriesMembers.Should().BeEmpty();
    }

    [Fact]
    public async Task The_workspace_shows_series_siblings_and_pt_scheme_context_for_the_analyte()
    {
        await using var db = NewDb();
        var created = await new CreatePrecisionSeriesHandler(db, Tenant(), new FakeRefGenerator())
            .Handle(new CreatePrecisionSeriesCommand("Glucose", "mmol/L",
                [new("Low", null, null), new("Mid", null, null), new("High", null, null)]), CancellationToken.None);

        var recorded = PtEnrollment.Enroll("PT-2026-0001", "CAP CHM", "GLUCOSE", "2026-A");
        recorded.RecordResult(5.6m, 5.5m, 0.22m, Guid.CreateVersion7());
        var otherAnalyte = PtEnrollment.Enroll("PT-2026-0002", "CAP CHM", "Urea", "2026-A");
        otherAnalyte.RecordResult(7m, 7m, 0.5m, Guid.CreateVersion7());
        var pending = PtEnrollment.Enroll("PT-2026-0003", "CAP CHM", "Glucose", "2026-B");
        db.PtEnrollments.AddRange(recorded, otherAnalyte, pending);
        await db.SaveChangesAsync();

        var detail = await new GetPrecisionStudyByIdHandler(db)
            .Handle(new GetPrecisionStudyByIdQuery(created.StudyIds[0]), CancellationToken.None);

        detail.SeriesLevelCount.Should().Be(3);
        detail.SeriesMembers.Select(m => m.Level).Should().BeEquivalentTo(["Mid", "High"]);
        var pt = detail.PtContext.Should().ContainSingle().Subject;
        pt.PtRef.Should().Be("PT-2026-0001");
        pt.SchemeCvPct.Should().Be(4m); // 0.22 / 5.5 · 100
    }

    [Fact]
    public async Task A_multi_site_import_reports_between_site_and_reproducibility_against_the_claim()
    {
        await using var db = NewDb();
        var id = await new CreatePrecisionStudyHandler(db, Tenant(), new FakeRefGenerator()).Handle(
            new CreatePrecisionStudyCommand("Glucose", "mmol/L", "Level 1", null, null, ClaimedReproducibilityCvPct: 45m),
            CancellationToken.None);

        var rows = new[]
        {
            ("Site A", "D1", 10m), ("Site A", "D1", 12m), ("Site A", "D2", 14m), ("Site A", "D2", 16m),
            ("Site B", "D1", 20m), ("Site B", "D1", 22m), ("Site B", "D2", 24m), ("Site B", "D2", 26m),
        }.Select(r => new AddPrecisionMeasurementRequest("R1", r.Item3, r.Item2, SiteLabel: r.Item1)).ToList();
        var imported = await new ImportPrecisionMeasurementsHandler(db)
            .Handle(new ImportPrecisionMeasurementsCommand(id, rows), CancellationToken.None);
        await new PrecisionWorkflowHandlers(db, new FakeCurrentUser(), new FixedClock(Now), null!)
            .Handle(new CalculatePrecisionCommand(id), CancellationToken.None);

        var detail = await new GetPrecisionStudyByIdHandler(db)
            .Handle(new GetPrecisionStudyByIdQuery(id), CancellationToken.None);

        imported.Imported.Should().Be(8);
        detail.Design.Should().Be(nameof(PrecisionDesign.SitesAndDays));
        detail.Coverage.Sites.Should().Be(2);
        detail.ClaimedReproducibilityCvPct.Should().Be(45m);
        detail.BetweenSiteSd.Should().BeApproximately(6.7823m, 0.0005m);
        detail.ReproducibilityCvPct.Should().BeApproximately(41.2011m, 0.001m);
        detail.MeetsReproducibilityClaim.Should().BeTrue();
        detail.WithinLabConsistentWithReproducibilityClaim.Should().BeNull();
        detail.Measurements.Should().OnlyContain(m => m.SiteLabel != null);
        detail.Runs.Select(r => r.SiteLabel).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Import_rejects_an_oversized_row_and_keeps_the_rest()
    {
        await using var db = NewDb();
        var study = PrecisionStudy.Configure("PR-2026-0001", "Glucose", "mmol/L", "Level 1", null, null);
        db.PrecisionStudies.Add(study);
        await db.SaveChangesAsync();

        var result = await new ImportPrecisionMeasurementsHandler(db).Handle(
            new ImportPrecisionMeasurementsCommand(study.Id,
            [
                new AddPrecisionMeasurementRequest("R1", 5.1m, "D1", ReagentLot: "LOT-1"),
                new AddPrecisionMeasurementRequest("R1", 5.2m, "D1", ReagentLot: new string('X', 61)),
                new AddPrecisionMeasurementRequest("", 5.3m, "D1"),
            ]), CancellationToken.None);

        result.Imported.Should().Be(1);
        result.Rejected.Select(r => r.Row).Should().Equal(2, 3);
    }

    [Fact]
    public async Task A_budget_imports_a_signed_off_within_lab_cv()
    {
        await using var db = NewDb();
        var study = await SignedOffStudyAsync(db);
        var budget = UncertaintyBudget.Create("MU-2026-0001", "Glucose", "Hexokinase", "mmol/L", "Level 1", 2m, null);
        db.UncertaintyBudgets.Add(budget);
        await db.SaveChangesAsync();

        await new AddPrecisionComponentHandler(db)
            .Handle(new AddPrecisionComponentCommand(budget.Id, study.Id), CancellationToken.None);

        var detail = await new GetUncertaintyBudgetByIdHandler(db)
            .Handle(new GetUncertaintyBudgetByIdQuery(budget.Id), CancellationToken.None);
        var component = detail.Components.Should().ContainSingle().Subject;
        component.SourcePrecisionStudyId.Should().Be(study.Id);
        component.RelativeStandardUncertainty.Should().Be(study.WithinLabCvPct!.Value);
    }

    [Fact]
    public async Task A_missing_precision_study_is_a_404_for_both_consumers()
    {
        await using var db = NewDb();
        var budget = UncertaintyBudget.Create("MU-2026-0001", "Glucose", "Hexokinase", "mmol/L", "Level 1", 2m, null);
        var sigma = SigmaAssessment.Create("SGA-2026-0001", "Glucose", "mmol/L", 10m, 1m, 2m);
        db.UncertaintyBudgets.Add(budget);
        db.SigmaAssessments.Add(sigma);
        await db.SaveChangesAsync();

        var mu = () => new AddPrecisionComponentHandler(db)
            .Handle(new AddPrecisionComponentCommand(budget.Id, Guid.CreateVersion7()), CancellationToken.None);
        var sig = () => new UseSigmaPrecisionStudyHandler(db)
            .Handle(new UseSigmaPrecisionStudyCommand(sigma.Id, Guid.CreateVersion7()), CancellationToken.None);

        (await mu.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("PR-404");
        (await sig.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("PR-404");
    }

    [Fact]
    public async Task A_sigma_assessment_takes_its_cv_from_the_study_and_reports_the_source()
    {
        await using var db = NewDb();
        var study = await SignedOffStudyAsync(db);
        var sigma = SigmaAssessment.Create("SGA-2026-0001", "Glucose", "mmol/L", 100m, 0m, 1m);
        db.SigmaAssessments.Add(sigma);
        await db.SaveChangesAsync();

        await new UseSigmaPrecisionStudyHandler(db)
            .Handle(new UseSigmaPrecisionStudyCommand(sigma.Id, study.Id), CancellationToken.None);

        var detail = await new GetSigmaAssessmentByIdHandler(db)
            .Handle(new GetSigmaAssessmentByIdQuery(sigma.Id), CancellationToken.None);
        detail.CvPct.Should().Be(study.WithinLabCvPct!.Value);
        detail.SourcePrecisionStudyId.Should().Be(study.Id);
        detail.SourcePrecisionStudyRef.Should().Be(study.StudyRef);
    }
}
