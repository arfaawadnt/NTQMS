using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreateDetectionLimitStudyCommand(
    string Analyte, string Unit, string Method, decimal LoqCvTargetPct) : ICommand<Guid>;

public sealed class CreateDetectionLimitStudyValidator : AbstractValidator<CreateDetectionLimitStudyCommand>
{
    public CreateDetectionLimitStudyValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Method).NotEmpty().MaximumLength(300);
        RuleFor(x => x.LoqCvTargetPct).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public sealed class CreateDetectionLimitStudyHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateDetectionLimitStudyCommand, Guid>
{
    public async Task<Guid> Handle(CreateDetectionLimitStudyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "DL", ct);
        var study = DetectionLimitStudy.Configure(studyRef, c.Analyte, c.Unit, c.Method, c.LoqCvTargetPct);
        db.DetectionLimitStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

public sealed record AddDetectionMeasurementCommand(
    Guid StudyId, string Kind, decimal? AssignedValue, decimal MeasuredValue) : ICommand<Guid>;
public sealed record RemoveDetectionMeasurementCommand(Guid StudyId, Guid MeasurementId) : ICommand;
public sealed record CalculateDetectionLimitCommand(Guid StudyId) : ICommand;
public sealed record SignOffDetectionLimitCommand(Guid StudyId) : ICommand;

public sealed class DetectionLimitWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddDetectionMeasurementCommand, Guid>,
    ICommandHandler<RemoveDetectionMeasurementCommand>,
    ICommandHandler<CalculateDetectionLimitCommand>,
    ICommandHandler<SignOffDetectionLimitCommand>
{
    public async Task<Guid> Handle(AddDetectionMeasurementCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        var id = study.AddMeasurement(
            Enum.Parse<DetectionSampleKind>(c.Kind, ignoreCase: true), c.AssignedValue, c.MeasuredValue);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveDetectionMeasurementCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.RemoveMeasurement(c.MeasurementId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateDetectionLimitCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffDetectionLimitCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var study = await LoadAsync(c.StudyId, ct);
        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<DetectionLimitStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.DetectionLimitStudies.Include(s => s.Measurements).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("DL-404", "Detection-limit study not found.");
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetDetectionLimitStudiesQuery(string? State)
    : IQuery<IReadOnlyList<DetectionLimitListItemDto>>;

public sealed class GetDetectionLimitStudiesHandler(IAppDbContext db)
    : IQueryHandler<GetDetectionLimitStudiesQuery, IReadOnlyList<DetectionLimitListItemDto>>
{
    public async Task<IReadOnlyList<DetectionLimitListItemDto>> Handle(
        GetDetectionLimitStudiesQuery q, CancellationToken ct)
    {
        var studies = db.DetectionLimitStudies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State)
            && Enum.TryParse<DetectionLimitState>(q.State, ignoreCase: true, out var state))
        {
            studies = studies.Where(s => s.State == state);
        }

        return await studies
            .OrderByDescending(s => s.StudyRef)
            .Select(s => new DetectionLimitListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.Method, s.State.ToString(),
                s.Lob, s.Lod, s.Loq))
            .ToListAsync(ct);
    }
}

public sealed record GetDetectionLimitStudyByIdQuery(Guid StudyId) : IQuery<DetectionLimitDetailDto>;

public sealed class GetDetectionLimitStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetDetectionLimitStudyByIdQuery, DetectionLimitDetailDto>
{
    public async Task<DetectionLimitDetailDto> Handle(GetDetectionLimitStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.DetectionLimitStudies.AsNoTracking()
            .Include(x => x.Measurements)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("DL-404", "Detection-limit study not found.");

        return new DetectionLimitDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.Method, s.LoqCvTargetPct, s.State.ToString(),
            s.BlankMean, s.BlankSd, s.PooledLowSd, s.Lob, s.Lod, s.Loq,
            s.SignedOffBy, s.SignedOffAtUtc,
            s.Measurements.OrderBy(m => m.Kind).ThenBy(m => m.AssignedValue)
                .Select(m => new DetectionMeasurementDto(m.Id, m.Kind.ToString(), m.AssignedValue, m.MeasuredValue))
                .ToList(),
            s.LowLevelAssessments()
                .Select(a => new LowLevelAssessmentDto(
                    a.AssignedValue, a.ReplicateCount, a.Mean, a.Sd, a.CvPct, a.QualifiesForLoq))
                .ToList());
    }
}
