using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

[RequireInternalActor]
public sealed record CreateInstrumentComparabilityCommand(
    string Analyte, string Unit, string ReferenceInstrument, decimal AllowableBiasPct) : ICommand<Guid>;

public sealed class CreateInstrumentComparabilityValidator : AbstractValidator<CreateInstrumentComparabilityCommand>
{
    public CreateInstrumentComparabilityValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.ReferenceInstrument).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AllowableBiasPct).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public sealed class CreateInstrumentComparabilityHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateInstrumentComparabilityCommand, Guid>
{
    public async Task<Guid> Handle(CreateInstrumentComparabilityCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "ICP", ct);
        var study = InstrumentComparabilityStudy.Configure(studyRef, c.Analyte, c.Unit, c.ReferenceInstrument, c.AllowableBiasPct);
        db.InstrumentComparabilities.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

[RequireInternalActor]
public sealed record AddInstrumentReadingCommand(Guid StudyId, string Instrument, string SampleId, decimal Value) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemoveInstrumentReadingCommand(Guid StudyId, Guid ReadingId) : ICommand;
[RequireInternalActor]
public sealed record CalculateInstrumentComparabilityCommand(Guid StudyId) : ICommand;
/// <summary>Signing off is a Part 11 signing ceremony (§11.200(a)(1)): it requires the signer's account password + e-signature PIN.</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.AnalyticalQuality,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record SignOffInstrumentComparabilityCommand(Guid StudyId, string Password, string Pin) : ICommand;

public sealed class InstrumentComparabilityWorkflowHandlers(
    IAppDbContext db, ICurrentUser user, IClock clock, IESignatureService signatures) :
    ICommandHandler<AddInstrumentReadingCommand, Guid>,
    ICommandHandler<RemoveInstrumentReadingCommand>,
    ICommandHandler<CalculateInstrumentComparabilityCommand>,
    ICommandHandler<SignOffInstrumentComparabilityCommand>
{
    public async Task<Guid> Handle(AddInstrumentReadingCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        var id = s.AddReading(c.Instrument, c.SampleId, c.Value);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveInstrumentReadingCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.RemoveReading(c.ReadingId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateInstrumentComparabilityCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffInstrumentComparabilityCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var s = await Load(c.StudyId, ct);

        // Pre-validate SoD + state BEFORE minting (append-only ledger; mirrors the NC verify pilot).
        if (s.CreatedByUserId is { } preparer && preparer == actor)
        {
            throw new DomainException(
                "SOD-AQ-001", "Segregation of duties: the preparer cannot sign off their own analytical record.");
        }

        if (s.State != InstrumentComparabilityState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "ICP-013", $"Only a calculated study can be signed off (current: {s.State}).");
        }

        var subjectRef = $"ICP:{s.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, "Signed off instrument comparability study", subjectRef,
            NT.QAMS.Application.Compliance.SignatureContentHash.Compute(
                ("subject", subjectRef), ("outcome", "signed-off")), ct);

        s.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<InstrumentComparabilityStudy> Load(Guid id, CancellationToken ct) =>
        await db.InstrumentComparabilities.Include(s => s.Readings).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("ICP-404", "Instrument-comparability study not found.");
}

public sealed record GetInstrumentComparabilitiesQuery(string? State) : IQuery<IReadOnlyList<InstrumentComparabilityListItemDto>>;

public sealed class GetInstrumentComparabilitiesHandler(IAppDbContext db)
    : IQueryHandler<GetInstrumentComparabilitiesQuery, IReadOnlyList<InstrumentComparabilityListItemDto>>
{
    public async Task<IReadOnlyList<InstrumentComparabilityListItemDto>> Handle(GetInstrumentComparabilitiesQuery q, CancellationToken ct)
    {
        var items = db.InstrumentComparabilities.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State) && Enum.TryParse<InstrumentComparabilityState>(q.State, true, out var st))
        {
            items = items.Where(s => s.State == st);
        }

        return await items.OrderByDescending(s => s.StudyRef)
            .Select(s => new InstrumentComparabilityListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.ReferenceInstrument, s.State.ToString(),
                s.InstrumentCount, s.NonComparableCount))
            .ToListAsync(ct);
    }
}

public sealed record GetInstrumentComparabilityByIdQuery(Guid StudyId) : IQuery<InstrumentComparabilityDetailDto>;

public sealed class GetInstrumentComparabilityByIdHandler(IAppDbContext db)
    : IQueryHandler<GetInstrumentComparabilityByIdQuery, InstrumentComparabilityDetailDto>
{
    public async Task<InstrumentComparabilityDetailDto> Handle(GetInstrumentComparabilityByIdQuery q, CancellationToken ct)
    {
        var s = await db.InstrumentComparabilities.AsNoTracking().Include(x => x.Readings)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("ICP-404", "Instrument-comparability study not found.");

        return new InstrumentComparabilityDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.ReferenceInstrument, s.AllowableBiasPct, s.State.ToString(),
            s.InstrumentCount, s.NonComparableCount, s.SignedOffBy, s.SignedOffAtUtc,
            s.Readings.OrderBy(r => r.Instrument).ThenBy(r => r.SampleId)
                .Select(r => new InstrumentReadingDto(r.Id, r.Instrument, r.SampleId, r.Value)).ToList(),
            s.Results().Select(r => new InstrumentResultDto(r.Instrument, r.PairedSamples, r.MeanBiasPct, r.Comparable)).ToList());
    }
}
