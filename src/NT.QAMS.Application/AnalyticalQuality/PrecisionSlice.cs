using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreatePrecisionStudyCommand(
    string Analyte, string Unit, string Level,
    decimal? ClaimedRepeatabilityCvPct, decimal? ClaimedWithinLabCvPct) : ICommand<Guid>;

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
        var studyRef = await refs.NextAsync(tenantId, "PR", ct);
        var study = PrecisionStudy.Configure(
            studyRef, c.Analyte, c.Unit, c.Level, c.ClaimedRepeatabilityCvPct, c.ClaimedWithinLabCvPct);
        db.PrecisionStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

public sealed record AddPrecisionMeasurementCommand(Guid StudyId, string RunLabel, decimal Value) : ICommand<Guid>;
public sealed record RemovePrecisionMeasurementCommand(Guid StudyId, Guid MeasurementId) : ICommand;
public sealed record CalculatePrecisionCommand(Guid StudyId) : ICommand;
public sealed record SignOffPrecisionCommand(Guid StudyId) : ICommand;

public sealed class PrecisionWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddPrecisionMeasurementCommand, Guid>,
    ICommandHandler<RemovePrecisionMeasurementCommand>,
    ICommandHandler<CalculatePrecisionCommand>,
    ICommandHandler<SignOffPrecisionCommand>
{
    public async Task<Guid> Handle(AddPrecisionMeasurementCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        var id = study.AddMeasurement(c.RunLabel, c.Value);
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
        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<PrecisionStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.PrecisionStudies.Include(s => s.Measurements).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("PR-404", "Precision study not found.");
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetPrecisionStudiesQuery(string? State)
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

        return await studies
            .OrderByDescending(s => s.StudyRef)
            .Select(s => new PrecisionListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.Level, s.State.ToString(),
                s.RepeatabilityCvPct, s.WithinLabCvPct, s.MeetsWithinLabClaim))
            .ToListAsync(ct);
    }
}

public sealed record GetPrecisionStudyByIdQuery(Guid StudyId) : IQuery<PrecisionDetailDto>;

public sealed class GetPrecisionStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetPrecisionStudyByIdQuery, PrecisionDetailDto>
{
    public async Task<PrecisionDetailDto> Handle(GetPrecisionStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.PrecisionStudies.AsNoTracking()
            .Include(x => x.Measurements)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("PR-404", "Precision study not found.");

        return new PrecisionDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.Level,
            s.ClaimedRepeatabilityCvPct, s.ClaimedWithinLabCvPct, s.State.ToString(),
            s.GrandMean, s.RepeatabilitySd, s.RepeatabilityCvPct, s.BetweenRunSd, s.BetweenRunCvPct,
            s.WithinLabSd, s.WithinLabCvPct, s.MeetsRepeatabilityClaim, s.MeetsWithinLabClaim,
            s.SignedOffBy, s.SignedOffAtUtc,
            s.Measurements.OrderBy(m => m.RunLabel)
                .Select(m => new PrecisionMeasurementDto(m.Id, m.RunLabel, m.Value)).ToList(),
            s.RunSummaries().Select(r => new PrecisionRunDto(r.RunLabel, r.ReplicateCount, r.Mean)).ToList());
    }
}
