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
public sealed record CreateSigmaAssessmentCommand(
    string Analyte, string Unit, decimal AllowableTotalErrorPct, decimal BiasPct, decimal CvPct) : ICommand<Guid>;

public sealed class CreateSigmaAssessmentValidator : AbstractValidator<CreateSigmaAssessmentCommand>
{
    public CreateSigmaAssessmentValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.AllowableTotalErrorPct).GreaterThan(0);
        RuleFor(x => x.CvPct).GreaterThan(0);
    }
}

public sealed class CreateSigmaAssessmentHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateSigmaAssessmentCommand, Guid>
{
    public async Task<Guid> Handle(CreateSigmaAssessmentCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var assessmentRef = await refs.NextAsync(tenantId, "SIG", ct);
        var assessment = SigmaAssessment.Create(
            assessmentRef, c.Analyte, c.Unit, c.AllowableTotalErrorPct, c.BiasPct, c.CvPct);
        db.SigmaAssessments.Add(assessment);
        await db.SaveChangesAsync(ct);
        return assessment.Id;
    }
}

[RequireInternalActor]
public sealed record UpdateSigmaInputsCommand(
    Guid AssessmentId, decimal AllowableTotalErrorPct, decimal BiasPct, decimal CvPct) : ICommand;
[RequireInternalActor]
public sealed record SignOffSigmaAssessmentCommand(Guid AssessmentId) : ICommand;

public sealed class SigmaAssessmentWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<UpdateSigmaInputsCommand>,
    ICommandHandler<SignOffSigmaAssessmentCommand>
{
    public async Task Handle(UpdateSigmaInputsCommand c, CancellationToken ct)
    {
        var assessment = await LoadAsync(c.AssessmentId, ct);
        assessment.SetInputs(c.AllowableTotalErrorPct, c.BiasPct, c.CvPct);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SignOffSigmaAssessmentCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var assessment = await LoadAsync(c.AssessmentId, ct);
        assessment.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<SigmaAssessment> LoadAsync(Guid id, CancellationToken ct) =>
        await db.SigmaAssessments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new DomainException("SIG-404", "Sigma assessment not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetSigmaAssessmentsQuery(string? State)
    : IQuery<IReadOnlyList<SigmaAssessmentListItemDto>>;

public sealed class GetSigmaAssessmentsHandler(IAppDbContext db)
    : IQueryHandler<GetSigmaAssessmentsQuery, IReadOnlyList<SigmaAssessmentListItemDto>>
{
    public async Task<IReadOnlyList<SigmaAssessmentListItemDto>> Handle(
        GetSigmaAssessmentsQuery q, CancellationToken ct)
    {
        var assessments = db.SigmaAssessments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State)
            && Enum.TryParse<SigmaAssessmentState>(q.State, ignoreCase: true, out var state))
        {
            assessments = assessments.Where(a => a.State == state);
        }

        return await assessments
            .OrderByDescending(a => a.AssessmentRef)
            .Select(a => new SigmaAssessmentListItemDto(
                a.Id, a.AssessmentRef, a.Analyte, a.AllowableTotalErrorPct, a.BiasPct, a.CvPct,
                a.SigmaValue, a.Grade.ToString(), a.State.ToString()))
            .ToListAsync(ct);
    }
}

public sealed record GetSigmaAssessmentByIdQuery(Guid AssessmentId) : IQuery<SigmaAssessmentDetailDto>;

public sealed class GetSigmaAssessmentByIdHandler(IAppDbContext db)
    : IQueryHandler<GetSigmaAssessmentByIdQuery, SigmaAssessmentDetailDto>
{
    public async Task<SigmaAssessmentDetailDto> Handle(GetSigmaAssessmentByIdQuery q, CancellationToken ct)
    {
        var a = await db.SigmaAssessments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.AssessmentId, ct)
            ?? throw new DomainException("SIG-404", "Sigma assessment not found.");

        return new SigmaAssessmentDetailDto(
            a.Id, a.AssessmentRef, a.Analyte, a.Unit,
            a.AllowableTotalErrorPct, a.BiasPct, a.CvPct,
            a.SigmaValue, a.Grade.ToString(), a.QcRecommendation, a.State.ToString(),
            a.SignedOffBy, a.SignedOffAtUtc);
    }
}
