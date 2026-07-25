using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreateReferenceIntervalStudyCommand(
    string Analyte, string Unit, string Population, string Source,
    decimal ClaimedLower, decimal ClaimedUpper) : ICommand<Guid>;

public sealed class CreateReferenceIntervalStudyValidator : AbstractValidator<CreateReferenceIntervalStudyCommand>
{
    public CreateReferenceIntervalStudyValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Population).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(300);
    }
}

public sealed class CreateReferenceIntervalStudyHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateReferenceIntervalStudyCommand, Guid>
{
    public async Task<Guid> Handle(CreateReferenceIntervalStudyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "RI", ct);
        var study = ReferenceIntervalStudy.Configure(
            studyRef, c.Analyte, c.Unit, c.Population, c.Source, c.ClaimedLower, c.ClaimedUpper);
        db.ReferenceIntervalStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

public sealed record AddReferenceSampleCommand(Guid StudyId, decimal Value, string? SubjectRef) : ICommand<Guid>;
public sealed record RemoveReferenceSampleCommand(Guid StudyId, Guid SampleId) : ICommand;
public sealed record CalculateReferenceIntervalCommand(Guid StudyId) : ICommand;
public sealed record SignOffReferenceIntervalCommand(Guid StudyId) : ICommand;

public sealed class ReferenceIntervalWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddReferenceSampleCommand, Guid>,
    ICommandHandler<RemoveReferenceSampleCommand>,
    ICommandHandler<CalculateReferenceIntervalCommand>,
    ICommandHandler<SignOffReferenceIntervalCommand>
{
    public async Task<Guid> Handle(AddReferenceSampleCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        var id = study.AddSample(c.Value, c.SubjectRef);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveReferenceSampleCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.RemoveSample(c.SampleId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateReferenceIntervalCommand c, CancellationToken ct)
    {
        var study = await LoadAsync(c.StudyId, ct);
        study.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffReferenceIntervalCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var study = await LoadAsync(c.StudyId, ct);
        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ReferenceIntervalStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ReferenceIntervalStudies.Include(s => s.Samples).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("RI-404", "Reference-interval study not found.");
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetReferenceIntervalStudiesQuery(string? State)
    : IQuery<IReadOnlyList<ReferenceIntervalListItemDto>>;

public sealed class GetReferenceIntervalStudiesHandler(IAppDbContext db)
    : IQueryHandler<GetReferenceIntervalStudiesQuery, IReadOnlyList<ReferenceIntervalListItemDto>>
{
    public async Task<IReadOnlyList<ReferenceIntervalListItemDto>> Handle(
        GetReferenceIntervalStudiesQuery q, CancellationToken ct)
    {
        var studies = db.ReferenceIntervalStudies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State)
            && Enum.TryParse<ReferenceIntervalState>(q.State, ignoreCase: true, out var state))
        {
            studies = studies.Where(s => s.State == state);
        }

        return await studies
            .OrderByDescending(s => s.StudyRef)
            .Select(s => new ReferenceIntervalListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.Population, s.ClaimedLower, s.ClaimedUpper, s.State.ToString(),
                s.OutsideCount, s.AllowedOutside, s.Verdict.HasValue ? s.Verdict.ToString() : null))
            .ToListAsync(ct);
    }
}

public sealed record GetReferenceIntervalStudyByIdQuery(Guid StudyId) : IQuery<ReferenceIntervalDetailDto>;

public sealed class GetReferenceIntervalStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetReferenceIntervalStudyByIdQuery, ReferenceIntervalDetailDto>
{
    public async Task<ReferenceIntervalDetailDto> Handle(GetReferenceIntervalStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.ReferenceIntervalStudies.AsNoTracking()
            .Include(x => x.Samples)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("RI-404", "Reference-interval study not found.");

        return new ReferenceIntervalDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.Population, s.Source,
            s.ClaimedLower, s.ClaimedUpper, s.State.ToString(),
            s.SampleCount, s.OutsideCount, s.AllowedOutside, s.Verdict.HasValue ? s.Verdict.ToString() : null,
            s.SignedOffBy, s.SignedOffAtUtc,
            s.Samples.OrderBy(x => x.Value)
                .Select(x => new ReferenceSampleDto(x.Id, x.Value, x.SubjectRef, s.IsOutside(x)))
                .ToList());
    }
}
