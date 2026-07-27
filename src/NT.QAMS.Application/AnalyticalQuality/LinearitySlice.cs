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
public sealed record CreateLinearityStudyCommand(
    string Analyte, string Unit, string Method, decimal AllowableDeviationPct) : ICommand<Guid>;

public sealed class CreateLinearityStudyValidator : AbstractValidator<CreateLinearityStudyCommand>
{
    public CreateLinearityStudyValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Method).NotEmpty().MaximumLength(300);
        RuleFor(x => x.AllowableDeviationPct).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public sealed class CreateLinearityStudyHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateLinearityStudyCommand, Guid>
{
    public async Task<Guid> Handle(CreateLinearityStudyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "LIN", ct);
        var study = LinearityStudy.Configure(studyRef, c.Analyte, c.Unit, c.Method, c.AllowableDeviationPct);
        db.LinearityStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

[RequireInternalActor]
public sealed record AddLinearityMeasurementCommand(
    Guid StudyId, decimal AssignedValue, decimal MeasuredValue) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemoveLinearityMeasurementCommand(Guid StudyId, Guid MeasurementId) : ICommand;
[RequireInternalActor]
public sealed record CalculateLinearityCommand(Guid StudyId) : ICommand;
[RequireInternalActor]
public sealed record SignOffLinearityCommand(Guid StudyId) : ICommand;

public sealed class LinearityWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddLinearityMeasurementCommand, Guid>,
    ICommandHandler<RemoveLinearityMeasurementCommand>,
    ICommandHandler<CalculateLinearityCommand>,
    ICommandHandler<SignOffLinearityCommand>
{
    public async Task<Guid> Handle(AddLinearityMeasurementCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        var id = study.AddMeasurement(c.AssignedValue, c.MeasuredValue);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveLinearityMeasurementCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.RemoveMeasurement(c.MeasurementId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateLinearityCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffLinearityCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var study = await LoadAsync(c.StudyId, ct);
        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<LinearityStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.LinearityStudies.Include(s => s.Measurements).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("LIN-404", "Linearity study not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetLinearityStudiesQuery(string? State)
    : IQuery<IReadOnlyList<LinearityListItemDto>>;

public sealed class GetLinearityStudiesHandler(IAppDbContext db)
    : IQueryHandler<GetLinearityStudiesQuery, IReadOnlyList<LinearityListItemDto>>
{
    public async Task<IReadOnlyList<LinearityListItemDto>> Handle(
        GetLinearityStudiesQuery q, CancellationToken ct)
    {
        var studies = db.LinearityStudies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State)
            && Enum.TryParse<LinearityState>(q.State, ignoreCase: true, out var state))
        {
            studies = studies.Where(s => s.State == state);
        }

        return await studies
            .OrderByDescending(s => s.StudyRef)
            .Select(s => new LinearityListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.Method, s.State.ToString(),
                s.IsLinear, s.AmrLow, s.AmrHigh, s.Slope, s.CorrelationR))
            .ToListAsync(ct);
    }
}

public sealed record GetLinearityStudyByIdQuery(Guid StudyId) : IQuery<LinearityDetailDto>;

public sealed class GetLinearityStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetLinearityStudyByIdQuery, LinearityDetailDto>
{
    public async Task<LinearityDetailDto> Handle(GetLinearityStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.LinearityStudies.AsNoTracking()
            .Include(x => x.Measurements)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("LIN-404", "Linearity study not found.");

        return new LinearityDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.Method, s.AllowableDeviationPct, s.State.ToString(),
            s.Slope, s.Intercept, s.CorrelationR, s.IsLinear, s.AmrLow, s.AmrHigh,
            s.SignedOffBy, s.SignedOffAtUtc,
            s.Measurements.OrderBy(m => m.AssignedValue)
                .Select(m => new LinearityMeasurementDto(m.Id, m.AssignedValue, m.MeasuredValue)).ToList(),
            s.LevelAssessments()
                .Select(a => new LinearityLevelDto(
                    a.AssignedValue, a.ReplicateCount, a.MeanMeasured, a.FittedValue,
                    a.DeviationPct, a.RecoveryPct, a.Passes)).ToList());
    }
}
