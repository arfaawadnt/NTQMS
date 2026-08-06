using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

[RequireInternalActor]
public sealed record CreateOutlierScreeningCommand(string Dataset, string Unit) : ICommand<Guid>;

public sealed class CreateOutlierScreeningValidator : AbstractValidator<CreateOutlierScreeningCommand>
{
    public CreateOutlierScreeningValidator()
    {
        RuleFor(x => x.Dataset).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
    }
}

public sealed class CreateOutlierScreeningHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateOutlierScreeningCommand, Guid>
{
    public async Task<Guid> Handle(CreateOutlierScreeningCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var screeningRef = await refs.NextAsync(tenantId, "OUT", ct);
        var screening = OutlierScreening.Configure(screeningRef, c.Dataset, c.Unit);
        db.OutlierScreenings.Add(screening);
        await db.SaveChangesAsync(ct);
        return screening.Id;
    }
}

[RequireInternalActor]
public sealed record AddOutlierPointCommand(Guid ScreeningId, decimal Value, string? Label) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemoveOutlierPointCommand(Guid ScreeningId, Guid PointId) : ICommand;
[RequireInternalActor]
public sealed record CalculateOutlierScreeningCommand(Guid ScreeningId) : ICommand;
/// <summary>Signing off is a Part 11 signing ceremony (§11.200(a)(1)): it requires the signer's account password + e-signature PIN.</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.AnalyticalQuality,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record SignOffOutlierScreeningCommand(Guid ScreeningId, string Password, string Pin) : ICommand;

public sealed class OutlierScreeningWorkflowHandlers(
    IAppDbContext db, ICurrentUser user, IClock clock, IESignatureService signatures) :
    ICommandHandler<AddOutlierPointCommand, Guid>,
    ICommandHandler<RemoveOutlierPointCommand>,
    ICommandHandler<CalculateOutlierScreeningCommand>,
    ICommandHandler<SignOffOutlierScreeningCommand>
{
    public async Task<Guid> Handle(AddOutlierPointCommand c, CancellationToken ct)
    {
        var s = await Load(c.ScreeningId, ct);
        var id = s.AddPoint(c.Value, c.Label);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveOutlierPointCommand c, CancellationToken ct)
    {
        var s = await Load(c.ScreeningId, ct);
        s.RemovePoint(c.PointId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateOutlierScreeningCommand c, CancellationToken ct)
    {
        var s = await Load(c.ScreeningId, ct);
        s.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffOutlierScreeningCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var s = await Load(c.ScreeningId, ct);

        // Pre-validate SoD + state BEFORE minting (append-only ledger; mirrors the NC verify pilot).
        if (s.CreatedByUserId is { } preparer && preparer == actor)
        {
            throw new DomainException(
                "SOD-AQ-001", "Segregation of duties: the preparer cannot sign off their own analytical record.");
        }

        if (s.State != OutlierScreeningState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "OUT-011", $"Only a calculated screening can be signed off (current: {s.State}).");
        }

        var subjectRef = $"OUT:{s.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, "Signed off outlier screening", subjectRef,
            NT.QAMS.Application.Compliance.SignatureContentHash.Compute(
                ("subject", subjectRef), ("outcome", "signed-off")), ct);

        s.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<OutlierScreening> Load(Guid id, CancellationToken ct) =>
        await db.OutlierScreenings.Include(s => s.Points).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("OUT-404", "Outlier screening not found.");
}

public sealed record GetOutlierScreeningsQuery(string? State) : IQuery<IReadOnlyList<OutlierScreeningListItemDto>>;

public sealed class GetOutlierScreeningsHandler(IAppDbContext db)
    : IQueryHandler<GetOutlierScreeningsQuery, IReadOnlyList<OutlierScreeningListItemDto>>
{
    public async Task<IReadOnlyList<OutlierScreeningListItemDto>> Handle(GetOutlierScreeningsQuery q, CancellationToken ct)
    {
        var items = db.OutlierScreenings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State) && Enum.TryParse<OutlierScreeningState>(q.State, true, out var st))
        {
            items = items.Where(s => s.State == st);
        }

        return await items.OrderByDescending(s => s.ScreeningRef)
            .Select(s => new OutlierScreeningListItemDto(
                s.Id, s.ScreeningRef, s.Dataset, s.State.ToString(), s.PointCount, s.OutlierCount))
            .ToListAsync(ct);
    }
}

public sealed record GetOutlierScreeningByIdQuery(Guid ScreeningId) : IQuery<OutlierScreeningDetailDto>;

public sealed class GetOutlierScreeningByIdHandler(IAppDbContext db)
    : IQueryHandler<GetOutlierScreeningByIdQuery, OutlierScreeningDetailDto>
{
    public async Task<OutlierScreeningDetailDto> Handle(GetOutlierScreeningByIdQuery q, CancellationToken ct)
    {
        var s = await db.OutlierScreenings.AsNoTracking().Include(x => x.Points)
            .FirstOrDefaultAsync(x => x.Id == q.ScreeningId, ct)
            ?? throw new DomainException("OUT-404", "Outlier screening not found.");

        return new OutlierScreeningDetailDto(
            s.Id, s.ScreeningRef, s.Dataset, s.Unit, s.State.ToString(),
            s.PointCount, s.Mean, s.Sd, s.Median, s.Q1, s.Q3, s.TukeyLower, s.TukeyUpper, s.OutlierCount,
            s.SignedOffBy, s.SignedOffAtUtc,
            s.PointResults().Select(r => new OutlierPointDto(r.Id, r.Value, r.Label, r.ZScore, r.ModifiedZScore, r.IsOutlier)).ToList());
    }
}
