using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreateMethodComparisonCommand(
    string Analyte, string Unit, string ReferenceMethod, string TestMethod) : ICommand<Guid>;

public sealed class CreateMethodComparisonValidator : AbstractValidator<CreateMethodComparisonCommand>
{
    public CreateMethodComparisonValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.ReferenceMethod).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TestMethod).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateMethodComparisonHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateMethodComparisonCommand, Guid>
{
    public async Task<Guid> Handle(CreateMethodComparisonCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "MC", ct);
        var study = MethodComparisonStudy.Configure(studyRef, c.Analyte, c.Unit, c.ReferenceMethod, c.TestMethod);
        db.MethodComparisons.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

public sealed record AddMeasurementPairCommand(
    Guid StudyId, decimal ReferenceValue, decimal TestValue, string? SampleId) : ICommand<Guid>;
public sealed record RemoveMeasurementPairCommand(Guid StudyId, Guid PairId) : ICommand;
public sealed record CalculateMethodComparisonCommand(Guid StudyId) : ICommand;
public sealed record SignOffMethodComparisonCommand(Guid StudyId) : ICommand;

public sealed class MethodComparisonWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddMeasurementPairCommand, Guid>,
    ICommandHandler<RemoveMeasurementPairCommand>,
    ICommandHandler<CalculateMethodComparisonCommand>,
    ICommandHandler<SignOffMethodComparisonCommand>
{
    public async Task<Guid> Handle(AddMeasurementPairCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        var id = study.AddPair(c.ReferenceValue, c.TestValue, c.SampleId);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveMeasurementPairCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.RemovePair(c.PairId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateMethodComparisonCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffMethodComparisonCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var study = await LoadAsync(c.StudyId, ct);
        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<MethodComparisonStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.MethodComparisons.Include(s => s.Pairs).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("MC-404", "Method-comparison study not found.");
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetMethodComparisonsQuery(string? State)
    : IQuery<IReadOnlyList<MethodComparisonListItemDto>>;

public sealed class GetMethodComparisonsHandler(IAppDbContext db)
    : IQueryHandler<GetMethodComparisonsQuery, IReadOnlyList<MethodComparisonListItemDto>>
{
    public async Task<IReadOnlyList<MethodComparisonListItemDto>> Handle(
        GetMethodComparisonsQuery q, CancellationToken ct)
    {
        var studies = db.MethodComparisons.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State)
            && Enum.TryParse<MethodComparisonState>(q.State, ignoreCase: true, out var state))
        {
            studies = studies.Where(s => s.State == state);
        }

        return await studies
            .OrderByDescending(s => s.StudyRef)
            .Select(s => new MethodComparisonListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.ReferenceMethod, s.TestMethod, s.State.ToString(),
                s.PairCount, s.DemingSlope, s.DemingIntercept, s.PearsonR, s.MeanBias))
            .ToListAsync(ct);
    }
}

public sealed record GetMethodComparisonByIdQuery(Guid StudyId) : IQuery<MethodComparisonDetailDto>;

public sealed class GetMethodComparisonByIdHandler(IAppDbContext db)
    : IQueryHandler<GetMethodComparisonByIdQuery, MethodComparisonDetailDto>
{
    public async Task<MethodComparisonDetailDto> Handle(GetMethodComparisonByIdQuery q, CancellationToken ct)
    {
        var s = await db.MethodComparisons.AsNoTracking()
            .Include(x => x.Pairs)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("MC-404", "Method-comparison study not found.");

        return new MethodComparisonDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.ReferenceMethod, s.TestMethod, s.State.ToString(),
            s.PairCount, s.PearsonR, s.DemingSlope, s.DemingIntercept,
            s.PassingBablokSlope, s.PassingBablokIntercept,
            s.MeanBias, s.BiasSd, s.LimitOfAgreementLower, s.LimitOfAgreementUpper,
            s.MeetsRecommendedPower, s.SignedOffBy, s.SignedOffAtUtc,
            s.Pairs.Select(p => new MeasurementPairDto(p.Id, p.ReferenceValue, p.TestValue, p.SampleId)).ToList());
    }
}
