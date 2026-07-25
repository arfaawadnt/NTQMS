using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

public sealed record CreateLotComparisonCommand(
    string Analyte, string Unit, string CurrentLot, string NewLot, decimal AllowableBiasPct) : ICommand<Guid>;

public sealed class CreateLotComparisonValidator : AbstractValidator<CreateLotComparisonCommand>
{
    public CreateLotComparisonValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.CurrentLot).NotEmpty().MaximumLength(60);
        RuleFor(x => x.NewLot).NotEmpty().MaximumLength(60);
        RuleFor(x => x.AllowableBiasPct).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public sealed class CreateLotComparisonHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateLotComparisonCommand, Guid>
{
    public async Task<Guid> Handle(CreateLotComparisonCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "LOT", ct);
        var study = LotComparisonStudy.Configure(studyRef, c.Analyte, c.Unit, c.CurrentLot, c.NewLot, c.AllowableBiasPct);
        db.LotComparisons.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

public sealed record AddLotPairCommand(Guid StudyId, decimal CurrentLotValue, decimal NewLotValue, string? SampleId) : ICommand<Guid>;
public sealed record RemoveLotPairCommand(Guid StudyId, Guid PairId) : ICommand;
public sealed record CalculateLotComparisonCommand(Guid StudyId) : ICommand;
public sealed record SignOffLotComparisonCommand(Guid StudyId) : ICommand;

public sealed class LotComparisonWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddLotPairCommand, Guid>,
    ICommandHandler<RemoveLotPairCommand>,
    ICommandHandler<CalculateLotComparisonCommand>,
    ICommandHandler<SignOffLotComparisonCommand>
{
    public async Task<Guid> Handle(AddLotPairCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        var id = s.AddPair(c.CurrentLotValue, c.NewLotValue, c.SampleId);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveLotPairCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.RemovePair(c.PairId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateLotComparisonCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffLotComparisonCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var s = await Load(c.StudyId, ct);
        s.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<LotComparisonStudy> Load(Guid id, CancellationToken ct) =>
        await db.LotComparisons.Include(s => s.Pairs).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("LOT-404", "Lot comparison not found.");
}

public sealed record GetLotComparisonsQuery(string? State) : IQuery<IReadOnlyList<LotComparisonListItemDto>>;

public sealed class GetLotComparisonsHandler(IAppDbContext db)
    : IQueryHandler<GetLotComparisonsQuery, IReadOnlyList<LotComparisonListItemDto>>
{
    public async Task<IReadOnlyList<LotComparisonListItemDto>> Handle(GetLotComparisonsQuery q, CancellationToken ct)
    {
        var items = db.LotComparisons.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State) && Enum.TryParse<LotComparisonState>(q.State, true, out var st))
        {
            items = items.Where(s => s.State == st);
        }

        return await items.OrderByDescending(s => s.StudyRef)
            .Select(s => new LotComparisonListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.CurrentLot, s.NewLot, s.State.ToString(), s.MeanBiasPct, s.Passes))
            .ToListAsync(ct);
    }
}

public sealed record GetLotComparisonByIdQuery(Guid StudyId) : IQuery<LotComparisonDetailDto>;

public sealed class GetLotComparisonByIdHandler(IAppDbContext db)
    : IQueryHandler<GetLotComparisonByIdQuery, LotComparisonDetailDto>
{
    public async Task<LotComparisonDetailDto> Handle(GetLotComparisonByIdQuery q, CancellationToken ct)
    {
        var s = await db.LotComparisons.AsNoTracking().Include(x => x.Pairs)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("LOT-404", "Lot comparison not found.");

        return new LotComparisonDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.CurrentLot, s.NewLot, s.AllowableBiasPct, s.State.ToString(),
            s.PairCount, s.MeanCurrent, s.MeanNew, s.MeanBiasPct, s.Passes, s.SignedOffBy, s.SignedOffAtUtc,
            s.Pairs.Select(p => new LotPairDto(p.Id, p.CurrentLotValue, p.NewLotValue, p.SampleId)).ToList());
    }
}
