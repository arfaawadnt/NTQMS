using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

[RequireInternalActor]
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

[RequireInternalActor]
public sealed record AddCarryoverReadingCommand(Guid StudyId, string Kind, int Sequence, decimal Value) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemoveCarryoverReadingCommand(Guid StudyId, Guid ReadingId) : ICommand;
[RequireInternalActor]
public sealed record CalculateCarryoverCommand(Guid StudyId) : ICommand;
/// <summary>Signing off is a Part 11 signing ceremony (§11.200(a)(1)): it requires the signer's account password + e-signature PIN.</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.AnalyticalQuality,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record SignOffCarryoverCommand(Guid StudyId, string Password, string Pin) : ICommand;

public sealed class CarryoverWorkflowHandlers(
    IAppDbContext db, ICurrentUser user, IClock clock, IESignatureService signatures) :
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

        // Pre-validate SoD + state BEFORE minting (append-only ledger; mirrors the NC verify pilot).
        if (s.CreatedByUserId is { } preparer && preparer == actor)
        {
            throw new DomainException(
                "SOD-AQ-001", "Segregation of duties: the preparer cannot sign off their own analytical record.");
        }

        if (s.State != CarryoverState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "CAR-013", $"Only a calculated study can be signed off (current: {s.State}).");
        }

        var subjectRef = $"CAR:{s.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, "Signed off carryover study", subjectRef,
            NT.QAMS.Application.Compliance.SignatureContentHash.Compute(
                ("subject", subjectRef), ("outcome", "signed-off")), ct);

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
