using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// ── Commands ────────────────────────────────────────────────────────────────

[RequireInternalActor]
public sealed record CreatePrecisionStudyCommand(
    string Analyte, string Unit, string Level,
    decimal? ClaimedRepeatabilityCvPct, decimal? ClaimedWithinLabCvPct,
    decimal? ClaimedReproducibilityCvPct = null) : ICommand<Guid>;

public sealed class CreatePrecisionStudyValidator : AbstractValidator<CreatePrecisionStudyCommand>
{
    public CreatePrecisionStudyValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Level).MaximumLength(100);
    }
}

public sealed class CreatePrecisionStudyHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreatePrecisionStudyCommand, Guid>
{
    public async Task<Guid> Handle(CreatePrecisionStudyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, PrecisionRefs.Study, ct);
        var study = PrecisionStudy.Configure(
            studyRef, c.Analyte, c.Unit, c.Level, c.ClaimedRepeatabilityCvPct, c.ClaimedWithinLabCvPct,
            null, 1, c.ClaimedReproducibilityCvPct);
        db.PrecisionStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

/// <summary>
/// Creates a multi-level series — one study per concentration level, linked by
/// a shared series reference, in one transaction. Each level stays its own
/// signed record (imprecision is level-dependent), and every member splits the
/// EP15 verification α across the series' level count.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
public sealed record CreatePrecisionSeriesCommand(
    string Analyte, string Unit, IReadOnlyList<PrecisionSeriesLevelRequest> Levels)
    : ICommand<PrecisionSeriesCreatedDto>;

public sealed class CreatePrecisionSeriesValidator : AbstractValidator<CreatePrecisionSeriesCommand>
{
    public CreatePrecisionSeriesValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Levels).NotNull()
            .Must(l => l.Count is >= 2 and <= PrecisionStudy.MaximumSeriesLevels)
            .WithMessage($"A series has 2–{PrecisionStudy.MaximumSeriesLevels} levels.")
            .Must(l => l.Select(x => x.Level.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == l.Count)
            .WithMessage("Each level of a series must be named distinctly.");
        RuleForEach(x => x.Levels).ChildRules(level =>
        {
            level.RuleFor(l => l.Level).NotEmpty().MaximumLength(100);
            level.RuleFor(l => l.ClaimedRepeatabilityCvPct).GreaterThan(0).When(l => l.ClaimedRepeatabilityCvPct is not null);
            level.RuleFor(l => l.ClaimedWithinLabCvPct).GreaterThan(0).When(l => l.ClaimedWithinLabCvPct is not null);
            level.RuleFor(l => l.ClaimedReproducibilityCvPct).GreaterThan(0).When(l => l.ClaimedReproducibilityCvPct is not null);
        });
    }
}

public sealed class CreatePrecisionSeriesHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreatePrecisionSeriesCommand, PrecisionSeriesCreatedDto>
{
    public async Task<PrecisionSeriesCreatedDto> Handle(CreatePrecisionSeriesCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var seriesRef = await refs.NextAsync(tenantId, PrecisionRefs.Series, ct);

        var ids = new List<Guid>(c.Levels.Count);
        foreach (var level in c.Levels)
        {
            var studyRef = await refs.NextAsync(tenantId, PrecisionRefs.Study, ct);
            var study = PrecisionStudy.Configure(
                studyRef, c.Analyte, c.Unit, level.Level,
                level.ClaimedRepeatabilityCvPct, level.ClaimedWithinLabCvPct,
                seriesRef, c.Levels.Count, level.ClaimedReproducibilityCvPct);
            db.PrecisionStudies.Add(study);
            ids.Add(study.Id);
        }

        await db.SaveChangesAsync(ct);
        return new PrecisionSeriesCreatedDto(seriesRef, ids);
    }
}

[RequireInternalActor]
public sealed record AddPrecisionMeasurementCommand(
    Guid StudyId, string RunLabel, decimal Value, string? DayLabel = null,
    string? Operator = null, string? Instrument = null, string? ReagentLot = null,
    string? CalibratorLot = null, DateOnly? MeasuredOn = null, string? SiteLabel = null) : ICommand<Guid>;

public sealed class AddPrecisionMeasurementValidator : AbstractValidator<AddPrecisionMeasurementCommand>
{
    public AddPrecisionMeasurementValidator()
    {
        RuleFor(x => x.RunLabel).NotEmpty().MaximumLength(PrecisionLimits.Label);
        RuleFor(x => x.DayLabel).MaximumLength(PrecisionLimits.Label);
        RuleFor(x => x.SiteLabel).MaximumLength(PrecisionLimits.Label);
        RuleFor(x => x.Operator).MaximumLength(PrecisionLimits.Condition);
        RuleFor(x => x.Instrument).MaximumLength(PrecisionLimits.Condition);
        RuleFor(x => x.ReagentLot).MaximumLength(PrecisionLimits.Lot);
        RuleFor(x => x.CalibratorLot).MaximumLength(PrecisionLimits.Lot);
    }
}

[RequireInternalActor]
public sealed record RemovePrecisionMeasurementCommand(Guid StudyId, Guid MeasurementId) : ICommand;
[RequireInternalActor]
public sealed record CalculatePrecisionCommand(Guid StudyId) : ICommand;
/// <summary>Signing off is a Part 11 signing ceremony (§11.200(a)(1)): it requires the signer's account password + e-signature PIN.</summary>
[RequirePermissionPolicy(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
public sealed record SignOffPrecisionCommand(Guid StudyId, string Password, string Pin) : ICommand;

public sealed class PrecisionWorkflowHandlers(
    IAppDbContext db, ICurrentUser user, IClock clock, IESignatureService signatures) :
    ICommandHandler<AddPrecisionMeasurementCommand, Guid>,
    ICommandHandler<RemovePrecisionMeasurementCommand>,
    ICommandHandler<CalculatePrecisionCommand>,
    ICommandHandler<SignOffPrecisionCommand>
{
    public async Task<Guid> Handle(AddPrecisionMeasurementCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        var id = study.AddMeasurement(
            c.RunLabel, c.Value, c.DayLabel,
            new PrecisionConditions(c.Operator, c.Instrument, c.ReagentLot, c.CalibratorLot, c.MeasuredOn),
            c.SiteLabel);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemovePrecisionMeasurementCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.RemoveMeasurement(c.MeasurementId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculatePrecisionCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffPrecisionCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var study = await LoadAsync(c.StudyId, ct);

        // Pre-validate SoD + state BEFORE minting (append-only ledger; mirrors the NC verify pilot).
        if (study.CreatedByUserId is { } preparer && preparer == actor)
        {
            throw new DomainException(
                "SOD-AQ-001", "Segregation of duties: the preparer cannot sign off their own analytical record.");
        }

        if (study.State != PrecisionState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "PR-012", $"Only a calculated study can be signed off (current: {study.State}).");
        }

        var subjectRef = $"PR:{study.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, "Signed off precision study", subjectRef,
            NT.QAMS.Application.Compliance.SignatureContentHash.Compute(
                ("subject", subjectRef), ("outcome", "signed-off")), ct);

        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<PrecisionStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.PrecisionStudies.Include(s => s.Measurements).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("PR-404", "Precision study not found.");
}

[RequireInternalActor]
public sealed record ImportPrecisionMeasurementsCommand(
    Guid StudyId, IReadOnlyList<AddPrecisionMeasurementRequest> Rows) : ICommand<BulkImportResultDto>;

/// <summary>
/// Bulk import of run-grouped replicates (analyzer/LIS export), optionally with
/// day labels and measurement conditions. Each row is validated and added
/// independently — a bad row is reported and skipped while the rest import;
/// the batch commits once.
/// </summary>
public sealed class ImportPrecisionMeasurementsHandler(IAppDbContext db)
    : ICommandHandler<ImportPrecisionMeasurementsCommand, BulkImportResultDto>
{
    public async Task<BulkImportResultDto> Handle(ImportPrecisionMeasurementsCommand c, CancellationToken ct)
    {
        var study = await db.PrecisionStudies.Include(s => s.Measurements)
            .FirstOrDefaultAsync(s => s.Id == c.StudyId, ct)
            ?? throw new DomainException("PR-404", "Precision study not found.");

        var imported = 0;
        var rejected = new List<BulkRejectDto>();
        for (var i = 0; i < c.Rows.Count; i++)
        {
            var row = c.Rows[i];
            if (PrecisionLimits.Violation(row) is { } violation)
            {
                rejected.Add(new BulkRejectDto(i + 1, violation));
                continue;
            }

            try
            {
                study.AddMeasurement(
                    row.RunLabel, row.Value, row.DayLabel,
                    new PrecisionConditions(row.Operator, row.Instrument, row.ReagentLot, row.CalibratorLot, row.MeasuredOn),
                    row.SiteLabel);
                imported++;
            }
            catch (DomainException ex)
            {
                rejected.Add(new BulkRejectDto(i + 1, ex.Message));
            }
        }

        if (imported > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new BulkImportResultDto(imported, rejected);
    }
}

/// <summary>Reference-number prefixes for precision studies and their series.</summary>
internal static class PrecisionRefs
{
    public const string Study = "PR";
    public const string Series = "PRS";
}

/// <summary>Column bounds of a precision replicate — the single source for validators and the CSV import.</summary>
internal static class PrecisionLimits
{
    public const int Label = 60;
    public const int Condition = 100;
    public const int Lot = 60;

    /// <summary>The first bound an imported row breaks, or null when it fits.</summary>
    public static string? Violation(AddPrecisionMeasurementRequest row) =>
        (row.RunLabel?.Length ?? 0) > Label || (row.DayLabel?.Length ?? 0) > Label || (row.SiteLabel?.Length ?? 0) > Label
            ? $"Site, day and run labels are limited to {Label} characters."
            : (row.Operator?.Length ?? 0) > Condition || (row.Instrument?.Length ?? 0) > Condition
                ? $"Operator and instrument are limited to {Condition} characters."
                : (row.ReagentLot?.Length ?? 0) > Lot || (row.CalibratorLot?.Length ?? 0) > Lot
                    ? $"Lot numbers are limited to {Lot} characters."
                    : null;
}

// ── Queries ─────────────────────────────────────────────────────────────────

/// <summary>The register, optionally narrowed by state and by analyte (case-insensitive exact match).</summary>
public sealed record GetPrecisionStudiesQuery(string? State, string? Analyte = null)
    : IQuery<IReadOnlyList<PrecisionListItemDto>>;

public sealed class GetPrecisionStudiesHandler(IAppDbContext db)
    : IQueryHandler<GetPrecisionStudiesQuery, IReadOnlyList<PrecisionListItemDto>>
{
    public async Task<IReadOnlyList<PrecisionListItemDto>> Handle(
        GetPrecisionStudiesQuery q, CancellationToken ct)
    {
        var studies = db.PrecisionStudies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State)
            && Enum.TryParse<PrecisionState>(q.State, ignoreCase: true, out var state))
        {
            studies = studies.Where(s => s.State == state);
        }

        if (!string.IsNullOrWhiteSpace(q.Analyte))
        {
            var analyte = q.Analyte.Trim().ToLower();
            studies = studies.Where(s => s.Analyte.ToLower() == analyte);
        }

        var rows = await studies
            .OrderByDescending(s => s.StudyRef)
            .Select(s => new
            {
                s.Id, s.StudyRef, s.Analyte, s.Level, s.State,
                s.RepeatabilityCvPct, s.WithinLabCvPct, s.MeetsWithinLabClaim, s.SeriesRef, s.Design,
            })
            .ToListAsync(ct);

        return rows.Select(s => new PrecisionListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.Level, s.State.ToString(),
                s.RepeatabilityCvPct, s.WithinLabCvPct, s.MeetsWithinLabClaim, s.SeriesRef, s.Design?.ToString()))
            .ToList();
    }
}

public sealed record GetPrecisionStudyByIdQuery(Guid StudyId) : IQuery<PrecisionDetailDto>;

/// <summary>
/// The study workspace. Besides the study itself it reads (query-side, same
/// module) the other levels of its series and the recorded PT/EQA results for
/// the analyte, whose scheme SD is the between-laboratory context.
/// </summary>
public sealed class GetPrecisionStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetPrecisionStudyByIdQuery, PrecisionDetailDto>
{
    /// <summary>How many recent PT results to show as between-laboratory context.</summary>
    public const int PtContextLimit = 10;

    public async Task<PrecisionDetailDto> Handle(GetPrecisionStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.PrecisionStudies.AsNoTracking()
            .Include(x => x.Measurements)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("PR-404", "Precision study not found.");

        var members = s.SeriesRef is null
            ? new List<PrecisionSeriesMemberDto>()
            : (await db.PrecisionStudies.AsNoTracking()
                    .Where(x => x.SeriesRef == s.SeriesRef && x.Id != s.Id)
                    .OrderBy(x => x.StudyRef)
                    .Select(x => new { x.Id, x.StudyRef, x.Level, x.State, x.WithinLabCvPct })
                    .ToListAsync(ct))
                .Select(x => new PrecisionSeriesMemberDto(x.Id, x.StudyRef, x.Level, x.State.ToString(), x.WithinLabCvPct))
                .ToList();

        var analyte = s.Analyte.ToLower();
        var pt = await db.PtEnrollments.AsNoTracking()
            .Where(p => p.Analyte.ToLower() == analyte
                        && p.AssignedValue != null && p.StandardDeviation != null)
            .OrderByDescending(p => p.PtRef)
            .Take(PtContextLimit)
            .Select(p => new { p.Id, p.PtRef, p.Scheme, p.Cycle, p.AssignedValue, p.StandardDeviation, p.ZScore })
            .ToListAsync(ct);

        var coverage = s.ConditionsCoverage();
        return new PrecisionDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.Level,
            s.ClaimedRepeatabilityCvPct, s.ClaimedWithinLabCvPct, s.State.ToString(),
            s.GrandMean, s.RepeatabilitySd, s.RepeatabilityCvPct, s.BetweenRunSd, s.BetweenRunCvPct,
            s.WithinLabSd, s.WithinLabCvPct, s.MeetsRepeatabilityClaim, s.MeetsWithinLabClaim,
            s.SignedOffBy, s.SignedOffAtUtc,
            s.Measurements
                .OrderBy(m => m.SiteLabel, StringComparer.Ordinal)
                .ThenBy(m => m.DayLabel, StringComparer.Ordinal)
                .ThenBy(m => m.RunLabel, StringComparer.Ordinal)
                .Select(m => new PrecisionMeasurementDto(
                    m.Id, m.RunLabel, m.Value, m.DayLabel,
                    m.Operator, m.Instrument, m.ReagentLot, m.CalibratorLot, m.MeasuredOn, m.SiteLabel))
                .ToList(),
            s.RunSummaries().Select(r => new PrecisionRunDto(r.DayLabel, r.RunLabel, r.ReplicateCount, r.Mean, r.SiteLabel)).ToList(),
            s.Design?.ToString(), s.BetweenDaySd, s.BetweenDayCvPct,
            s.RepeatabilityDf, s.RepeatabilityCvLowerPct, s.RepeatabilityCvUpperPct,
            s.WithinLabDf, s.WithinLabCvLowerPct, s.WithinLabCvUpperPct,
            s.RepeatabilityVerificationLimitCvPct, s.WithinLabVerificationLimitCvPct,
            s.SeriesRef, s.SeriesLevelCount,
            new PrecisionCoverageDto(
                coverage.Days, coverage.Runs, coverage.Operators, coverage.Instruments,
                coverage.ReagentLots, coverage.CalibratorLots,
                coverage.FirstMeasuredOn, coverage.LastMeasuredOn, coverage.MeetsEp15MinimumDesign,
                coverage.Sites, coverage.MeetsMultiSiteMinimumDesign),
            members,
            pt.Select(p => new PrecisionPtContextDto(
                    p.Id, p.PtRef, p.Scheme, p.Cycle, p.AssignedValue!.Value, p.StandardDeviation!.Value,
                    p.AssignedValue!.Value == 0m
                        ? null
                        : Math.Round(p.StandardDeviation!.Value / Math.Abs(p.AssignedValue!.Value) * 100m, 2),
                    p.ZScore))
                .ToList(),
            s.ClaimedReproducibilityCvPct,
            s.BetweenSiteSd, s.BetweenSiteCvPct,
            s.ReproducibilitySd, s.ReproducibilityCvPct, s.ReproducibilityDf,
            s.ReproducibilityCvLowerPct, s.ReproducibilityCvUpperPct,
            s.ReproducibilityVerificationLimitCvPct,
            s.MeetsReproducibilityClaim, s.WithinLabConsistentWithReproducibilityClaim);
    }
}
