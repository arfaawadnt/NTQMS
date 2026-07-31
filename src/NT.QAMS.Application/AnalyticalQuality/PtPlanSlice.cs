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
public sealed record CreatePtPlanCommand(int Year) : ICommand<Guid>;
[RequireInternalActor]
public sealed record AddPtPlanItemCommand(
    Guid PlanId, string Scheme, string Analyte, string? Provider, int PlannedCycles, string? Notes) : ICommand<Guid>;
[RequireInternalActor]
public sealed record RemovePtPlanItemCommand(Guid PlanId, Guid ItemId) : ICommand;
[RequireInternalActor]
public sealed record ApprovePtPlanCommand(Guid PlanId) : ICommand;
[RequireInternalActor]
public sealed record RecordPtPlanFulfilmentCommand(Guid PlanId, Guid ItemId, Guid EnrollmentId) : ICommand;
[RequireInternalActor]
public sealed record ClosePtPlanCommand(Guid PlanId, string ClosureSummary) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class ClosePtPlanValidator : AbstractValidator<ClosePtPlanCommand>
{
    public ClosePtPlanValidator()
    {
        RuleFor(x => x.ClosureSummary).NotEmpty().MaximumLength(4000);
    }
}

public sealed class AddPtPlanItemValidator : AbstractValidator<AddPtPlanItemCommand>
{
    public AddPtPlanItemValidator()
    {
        RuleFor(x => x.Scheme).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Provider).MaximumLength(200);
        RuleFor(x => x.PlannedCycles).InclusiveBetween(1, 52);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class CreatePtPlanHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreatePtPlanCommand, Guid>
{
    public async Task<Guid> Handle(CreatePtPlanCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        if (await db.PtPlans.AnyAsync(p => p.Year == c.Year, ct))
        {
            throw new DomainException("PTP-020", $"A PT plan for {c.Year} already exists â€” one plan per year.");
        }

        var planRef = await refs.NextAsync(tenantId, "PTP", ct);
        var plan = PtPlan.Create(planRef, c.Year);
        db.PtPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan.Id;
    }
}

public sealed class PtPlanWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddPtPlanItemCommand, Guid>,
    ICommandHandler<RemovePtPlanItemCommand>,
    ICommandHandler<ApprovePtPlanCommand>,
    ICommandHandler<RecordPtPlanFulfilmentCommand>,
    ICommandHandler<ClosePtPlanCommand>
{
    public async Task<Guid> Handle(AddPtPlanItemCommand c, CancellationToken ct)
    {
        var plan = await LoadAsync(c.PlanId, ct);
        var id = plan.AddItem(c.Scheme, c.Analyte, c.Provider, c.PlannedCycles, c.Notes);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemovePtPlanItemCommand c, CancellationToken ct)
    {
        var plan = await LoadAsync(c.PlanId, ct);
        plan.RemoveItem(c.ItemId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ApprovePtPlanCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var plan = await LoadAsync(c.PlanId, ct);
        plan.Approve(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RecordPtPlanFulfilmentCommand c, CancellationToken ct)
    {
        // Only a resulted enrollment counts as a fulfilled cycle.
        var enrollment = await db.PtEnrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == c.EnrollmentId, ct)
            ?? throw new DomainException("PT-404", "PT enrollment not found.");
        if (enrollment.Performance == PtPerformance.Pending)
        {
            throw new DomainException("PTP-021", $"Enrollment {enrollment.PtRef} has no result yet â€” a pending cycle is not fulfilment.");
        }

        var plan = await LoadAsync(c.PlanId, ct);
        plan.RecordFulfilment(c.ItemId, enrollment.PtRef);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ClosePtPlanCommand c, CancellationToken ct)
    {
        var plan = await LoadAsync(c.PlanId, ct);
        plan.Close(c.ClosureSummary);
        await db.SaveChangesAsync(ct);
    }

    private async Task<PtPlan> LoadAsync(Guid id, CancellationToken ct) =>
        await db.PtPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("PTP-404", "PT plan not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetPtPlansQuery : IQuery<IReadOnlyList<PtPlanListItemDto>>;

public sealed class GetPtPlansHandler(IAppDbContext db)
    : IQueryHandler<GetPtPlansQuery, IReadOnlyList<PtPlanListItemDto>>
{
    public async Task<IReadOnlyList<PtPlanListItemDto>> Handle(GetPtPlansQuery q, CancellationToken ct) =>
        await db.PtPlans.AsNoTracking()
            .OrderByDescending(p => p.Year)
            .Select(p => new PtPlanListItemDto(
                p.Id, p.PlanRef, p.Year, p.Status.ToString(),
                p.Items.Count,
                p.Items.Sum(i => i.PlannedCycles),
                p.Items.Sum(i => i.FulfilledCycles)))
            .ToListAsync(ct);
}

public sealed record GetPtPlanByIdQuery(Guid PlanId) : IQuery<PtPlanDetailDto>;

public sealed class GetPtPlanByIdHandler(IAppDbContext db)
    : IQueryHandler<GetPtPlanByIdQuery, PtPlanDetailDto>
{
    public async Task<PtPlanDetailDto> Handle(GetPtPlanByIdQuery q, CancellationToken ct)
    {
        var p = await db.PtPlans.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == q.PlanId, ct)
            ?? throw new DomainException("PTP-404", "PT plan not found.");

        return new PtPlanDetailDto(
            p.Id, p.PlanRef, p.Year, p.Status.ToString(), p.ApprovedBy, p.ApprovedAtUtc, p.ClosureSummary,
            p.Items.OrderBy(i => i.Scheme).ThenBy(i => i.Analyte)
                .Select(i => new PtPlanItemDto(
                    i.Id, i.Scheme, i.Analyte, i.Provider, i.PlannedCycles, i.FulfilledCycles,
                    i.LastEnrollmentRef, i.Notes))
                .ToList());
    }
}
