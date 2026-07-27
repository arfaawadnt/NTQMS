using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

[RequireInternalActor]
public sealed record CreateInterferenceStudyCommand(string Analyte, string Unit, decimal AllowableBiasPct) : ICommand<Guid>;

public sealed class CreateInterferenceStudyValidator : AbstractValidator<CreateInterferenceStudyCommand>
{
    public CreateInterferenceStudyValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.AllowableBiasPct).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public sealed class CreateInterferenceStudyHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateInterferenceStudyCommand, Guid>
{
    public async Task<Guid> Handle(CreateInterferenceStudyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "INT", ct);
        var study = InterferenceStudy.Configure(studyRef, c.Analyte, c.Unit, c.AllowableBiasPct);
        db.InterferenceStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

/// <summary>Kind = "Control" (interferent ignored) or "Test" (interferent required).</summary>
[RequireInternalActor]
public sealed record AddInterferenceMeasurementCommand(Guid StudyId, string Kind, string? Interferent, decimal Value) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemoveInterferenceMeasurementCommand(Guid StudyId, Guid MeasurementId) : ICommand;
[RequireInternalActor]
public sealed record CalculateInterferenceCommand(Guid StudyId) : ICommand;
[RequireInternalActor]
public sealed record SignOffInterferenceCommand(Guid StudyId) : ICommand;

public sealed class InterferenceWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddInterferenceMeasurementCommand, Guid>,
    ICommandHandler<RemoveInterferenceMeasurementCommand>,
    ICommandHandler<CalculateInterferenceCommand>,
    ICommandHandler<SignOffInterferenceCommand>
{
    public async Task<Guid> Handle(AddInterferenceMeasurementCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        var id = string.Equals(c.Kind, "Control", StringComparison.OrdinalIgnoreCase)
            ? s.AddControl(c.Value)
            : s.AddTest(c.Interferent ?? string.Empty, c.Value);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveInterferenceMeasurementCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.RemoveMeasurement(c.MeasurementId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateInterferenceCommand c, CancellationToken ct)
    {
        var s = await Load(c.StudyId, ct);
        s.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffInterferenceCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var s = await Load(c.StudyId, ct);
        s.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<InterferenceStudy> Load(Guid id, CancellationToken ct) =>
        await db.InterferenceStudies.Include(s => s.Measurements).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("INT-404", "Interference study not found.");
}

public sealed record GetInterferenceStudiesQuery(string? State) : IQuery<IReadOnlyList<InterferenceListItemDto>>;

public sealed class GetInterferenceStudiesHandler(IAppDbContext db)
    : IQueryHandler<GetInterferenceStudiesQuery, IReadOnlyList<InterferenceListItemDto>>
{
    public async Task<IReadOnlyList<InterferenceListItemDto>> Handle(GetInterferenceStudiesQuery q, CancellationToken ct)
    {
        var items = db.InterferenceStudies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State) && Enum.TryParse<InterferenceState>(q.State, true, out var st))
        {
            items = items.Where(s => s.State == st);
        }

        return await items.OrderByDescending(s => s.StudyRef)
            .Select(s => new InterferenceListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.State.ToString(), s.InterferentCount, s.SignificantCount))
            .ToListAsync(ct);
    }
}

public sealed record GetInterferenceStudyByIdQuery(Guid StudyId) : IQuery<InterferenceDetailDto>;

public sealed class GetInterferenceStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetInterferenceStudyByIdQuery, InterferenceDetailDto>
{
    public async Task<InterferenceDetailDto> Handle(GetInterferenceStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.InterferenceStudies.AsNoTracking().Include(x => x.Measurements)
            .FirstOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("INT-404", "Interference study not found.");

        return new InterferenceDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Unit, s.AllowableBiasPct, s.State.ToString(),
            s.ControlMean, s.InterferentCount, s.SignificantCount, s.SignedOffBy, s.SignedOffAtUtc,
            s.Measurements.OrderByDescending(m => m.IsControl).ThenBy(m => m.Interferent)
                .Select(m => new InterferenceMeasurementDto(m.Id, m.IsControl, m.Interferent, m.Value)).ToList(),
            s.Results().Select(r => new InterferenceResultDto(
                r.Interferent, r.ReplicateCount, r.MeanTest, r.BiasPct, r.SignificantInterference)).ToList());
    }
}
