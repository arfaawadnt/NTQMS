using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

public sealed record CreateCarryoverStudyCommand(string Analyte, string Unit, decimal AllowableCarryoverPct) : ICommand<Guid>;

public sealed class CreateCarryoverStudyValidator : AbstractValidator<CreateCarryoverStudyCommand>
{
    public CreateCarryoverStudyValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.AllowableCarryoverPct).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public sealed class CreateCarryoverStudyHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateCarryoverStudyCommand, Guid>
{
    public async Task<Guid> Handle(CreateCarryoverStudyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "CAR", ct);
        var study = CarryoverStudy.Configure(studyRef, c.Analyte, c.Unit, c.AllowableCarryoverPct);
        db.CarryoverStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

public sealed record AddCarryoverReadingCommand(Guid StudyId, string Kind, int Sequence, decimal Value) : ICommand<Guid>;
public sealed record RemoveCarryoverReadingCommand(Guid StudyId, Guid ReadingId) : ICommand;
public sealed record CalculateCarryoverCommand(Guid StudyId) : ICommand;
public sealed record SignOffCarryoverCommand(Guid StudyId) : ICommand;

public sealed class CarryoverWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddCarryoverReadingCommand, Guid>,
    ICommandHandler<RemoveCarryoverReadingCommand>,
    ICommandHandler<CalculateCarryoverCommand>,
    ICommandHandler<SignOffCarryoverCommand>
{
    public async Task<Guid> Handle(AddCarryoverReadingCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        var id = s.AddReading(Enum.Parse<CarryoverSampleKind>(c.Kind, true), c.Sequence, c.Value);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveCarryoverReadingCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.RemoveReading(c.ReadingId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateCarryoverCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffCarryoverCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var s = await Load(c.StudyId, ct);
        s.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<CarryoverStudy> Load(Guid id, CancellationToken ct) =>
        await db.CarryoverStudies.Include(s => s.Readings).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("CAR-404", "Carryover study not found.");
}

public sealed record GetCarryoverStudiesQuery(string? State) : IQuery<IReadOnlyList<CarryoverListItemDto>>;

public sealed class GetCarryoverStudiesHandler(IAppDbContext db)
    : IQueryHandler<GetCarryoverStudiesQuery, IReadOnlyList<CarryoverListItemDto>>
{
    public async Task<IReadOnlyList<CarryoverListItemDto>> Handle(GetCarryoverStudiesQuery q, CancellationToken ct)
    {
        var items = db.CarryoverStudies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State) && Enum.TryParse<CarryoverState>(q.State, true, out var st))
        {
            items = items.Where(s => s.State == st);
        }

        return await items.OrderByDescending(s => s.StudyRef)
            .Select(s => new CarryoverListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.State.ToString(), s.CarryoverPct, s.Passes))
            .ToListAsync(ct);
    }
}

public sealed record GetCarryoverStudyByIdQuery(Guid StudyId) : IQuery<CarryoverDetailDto>;

public sealed class GetCarryoverStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetCarryoverStudyByIdQuery, CarryoverDetailDto>
{
    public async Task<CarryoverDetailDto> Handle(GetCarryoverStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.CarryoverStudies.AsNoTracking().Include(x => x.Readings)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("CAR-404", "Carryover study not found.");

        return new CarryoverDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.AllowableCarryoverPct, s.State.ToString(),
            s.MeanHigh, s.FirstLow, s.SteadyLow, s.CarryoverPct, s.Passes, s.SignedOffBy, s.SignedOffAtUtc,
            s.Readings.OrderBy(r => r.Kind).ThenBy(r => r.Sequence)
                .Select(r => new CarryoverReadingDto(r.Id, r.Kind.ToString(), r.Sequence, r.Value)).ToList());
    }
}
