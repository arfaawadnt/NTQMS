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

[RequireInternalActor]
public sealed record AddMeasurementPairCommand(
    Guid StudyId, decimal ReferenceValue, decimal TestValue, string? SampleId) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemoveMeasurementPairCommand(Guid StudyId, Guid PairId) : ICommand;
[RequireInternalActor]
public sealed record CalculateMethodComparisonCommand(Guid StudyId) : ICommand;
/// <summary>Signing off is a Part 11 signing ceremony (§11.200(a)(1)): it requires the signer's account password + e-signature PIN.</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.AnalyticalQuality,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record SignOffMethodComparisonCommand(Guid StudyId, string Password, string Pin) : ICommand;

public sealed class MethodComparisonWorkflowHandlers(
    IAppDbContext db, ICurrentUser user, IClock clock, IESignatureService signatures) :
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

        // Pre-validate SoD + state BEFORE minting (append-only ledger; mirrors the NC verify pilot).
        if (study.CreatedByUserId is { } preparer && preparer == actor)
        {
            throw new DomainException(
                "SOD-AQ-001", "Segregation of duties: the preparer cannot sign off their own analytical record.");
        }

        if (study.State != MethodComparisonState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "MC-012", $"Only a calculated study can be signed off (current: {study.State}).");
        }

        var subjectRef = $"MC:{study.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, "Signed off method comparison study", subjectRef,
            NT.QAMS.Application.Compliance.SignatureContentHash.Compute(
                ("subject", subjectRef), ("outcome", "signed-off")), ct);

        study.SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<MethodComparisonStudy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.MethodComparisons.Include(s => s.Pairs).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("MC-404", "Method-comparison study not found.");
}

[RequireInternalActor]
public sealed record ImportMeasurementPairsCommand(
    Guid StudyId, IReadOnlyList<AddMeasurementPairRequest> Rows) : ICommand<BulkImportResultDto>;

/// <summary>
/// Bulk import of paired results (analyzer/LIS export). Each row is validated
/// and added independently: a bad row is reported with a reason and skipped,
/// the rest still import (partial import with an integrity report). The whole
/// batch commits once.
/// </summary>
public sealed class ImportMeasurementPairsHandler(IAppDbContext db)
    : ICommandHandler<ImportMeasurementPairsCommand, BulkImportResultDto>
{
    public async Task<BulkImportResultDto> Handle(ImportMeasurementPairsCommand c, CancellationToken ct)
    {
        var study = await db.MethodComparisons.Include(s => s.Pairs)
            .FirstOrDefaultAsync(s => s.Id == c.StudyId, ct)
            ?? throw new DomainException("MC-404", "Method-comparison study not found.");

        var imported = 0;
        var rejected = new List<BulkRejectDto>();
        for (var i = 0; i < c.Rows.Count; i++)
        {
            var row = c.Rows[i];
            try
            {
                study.AddPair(row.ReferenceValue, row.TestValue, row.SampleId);
                imported++;
            }
            catch (DomainException ex)
            {
                rejected.Add(new BulkRejectDto(i + 1, ex.Message));
            }
        }

        if (imported > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new BulkImportResultDto(imported, rejected);
    }
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
