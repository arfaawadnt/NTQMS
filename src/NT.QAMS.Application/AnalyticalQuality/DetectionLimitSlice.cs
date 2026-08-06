using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
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

[RequireInternalActor]
public sealed record AddDetectionMeasurementCommand(
    Guid StudyId, string Kind, decimal? AssignedValue, decimal MeasuredValue) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemoveDetectionMeasurementCommand(Guid StudyId, Guid MeasurementId) : ICommand;
[RequireInternalActor]
public sealed record CalculateDetectionLimitCommand(Guid StudyId) : ICommand;
/// <summary>Signing off is a Part 11 signing ceremony (§11.200(a)(1)): it requires the signer's account password + e-signature PIN.</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.AnalyticalQuality,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record SignOffDetectionLimitCommand(Guid StudyId, string Password, string Pin) : ICommand;

public sealed class DetectionLimitWorkflowHandlers(
    IAppDbContext db, ICurrentUser user, IClock clock, IESignatureService signatures) :
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

        // Pre-validate SoD + state BEFORE minting (append-only ledger; mirrors the NC verify pilot).
        if (study.CreatedByUserId is { } preparer && preparer == actor)
        {
            throw new DomainException(
                "SOD-AQ-001", "Segregation of duties: the preparer cannot sign off their own analytical record.");
        }

        if (study.State != DetectionLimitState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "DL-013", $"Only a calculated study can be signed off (current: {study.State}).");
        }

        var subjectRef = $"DL:{study.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, "Signed off detection limit study", subjectRef,
            NT.QAMS.Application.Compliance.SignatureContentHash.Compute(
                ("subject", subjectRef), ("outcome", "signed-off")), ct);

        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<DetectionLimitStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.DetectionLimitStudies.Include(s => s.Measurements).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("DL-404", "Detection-limit study not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
