using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreateUncertaintyBudgetCommand(
    string Analyte, string Method, string Unit, string Level,
    decimal CoverageFactor, decimal? TargetExpandedUncertainty) : ICommand<Guid>;

public sealed class CreateUncertaintyBudgetValidator : AbstractValidator<CreateUncertaintyBudgetCommand>
{
    public CreateUncertaintyBudgetValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Method).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Level).MaximumLength(100);
        RuleFor(x => x.CoverageFactor).InclusiveBetween(1, 4);
    }
}

public sealed class CreateUncertaintyBudgetHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateUncertaintyBudgetCommand, Guid>
{
    public async Task<Guid> Handle(CreateUncertaintyBudgetCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var budgetRef = await refs.NextAsync(tenantId, "MU", ct);
        var budget = UncertaintyBudget.Create(
            budgetRef, c.Analyte, c.Method, c.Unit, c.Level, c.CoverageFactor, c.TargetExpandedUncertainty);
        db.UncertaintyBudgets.Add(budget);
        await db.SaveChangesAsync(ct);
        return budget.Id;
    }
}

public sealed record AddUncertaintyComponentCommand(
    Guid BudgetId, string Name, string Type, decimal RelativeStandardUncertainty, string? Source) : ICommand<Guid>;
public sealed record RemoveUncertaintyComponentCommand(Guid BudgetId, Guid ComponentId) : ICommand;
public sealed record CalculateUncertaintyBudgetCommand(Guid BudgetId) : ICommand;
public sealed record ApproveUncertaintyBudgetCommand(Guid BudgetId) : ICommand;

public sealed class AddUncertaintyComponentValidator : AbstractValidator<AddUncertaintyComponentCommand>
{
    public AddUncertaintyComponentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.RelativeStandardUncertainty).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Source).MaximumLength(500);
    }
}

public sealed class UncertaintyWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<AddUncertaintyComponentCommand, Guid>,
    ICommandHandler<RemoveUncertaintyComponentCommand>,
    ICommandHandler<CalculateUncertaintyBudgetCommand>,
    ICommandHandler<ApproveUncertaintyBudgetCommand>
{
    public async Task<Guid> Handle(AddUncertaintyComponentCommand c, CancellationToken ct)
    {
        var budget = await LoadAsync(c.BudgetId, ct);
        var id = budget.AddComponent(
            c.Name, Enum.Parse<UncertaintyComponentType>(c.Type, ignoreCase: true),
            c.RelativeStandardUncertainty, c.Source);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(RemoveUncertaintyComponentCommand c, CancellationToken ct)
    {
        var budget = await LoadAsync(c.BudgetId, ct);
        budget.RemoveComponent(c.ComponentId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CalculateUncertaintyBudgetCommand c, CancellationToken ct)
    {
        var budget = await LoadAsync(c.BudgetId, ct);
        budget.Calculate();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ApproveUncertaintyBudgetCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var budget = await LoadAsync(c.BudgetId, ct);
        budget.Approve(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<UncertaintyBudget> LoadAsync(Guid id, CancellationToken ct) =>
        await db.UncertaintyBudgets.Include(b => b.Components).FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new DomainException("MU-404", "Uncertainty budget not found.");
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetUncertaintyBudgetsQuery(string? Status)
    : IQuery<IReadOnlyList<UncertaintyBudgetListItemDto>>;

public sealed class GetUncertaintyBudgetsHandler(IAppDbContext db)
    : IQueryHandler<GetUncertaintyBudgetsQuery, IReadOnlyList<UncertaintyBudgetListItemDto>>
{
    public async Task<IReadOnlyList<UncertaintyBudgetListItemDto>> Handle(
        GetUncertaintyBudgetsQuery q, CancellationToken ct)
    {
        var budgets = db.UncertaintyBudgets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<UncertaintyBudgetStatus>(q.Status, ignoreCase: true, out var status))
        {
            budgets = budgets.Where(b => b.Status == status);
        }

        return await budgets
            .OrderByDescending(b => b.BudgetRef)
            .Select(b => new UncertaintyBudgetListItemDto(
                b.Id, b.BudgetRef, b.Analyte, b.Method, b.Level, b.Status.ToString(),
                b.ExpandedUncertainty, b.MeetsTarget))
            .ToListAsync(ct);
    }
}

public sealed record GetUncertaintyBudgetByIdQuery(Guid BudgetId) : IQuery<UncertaintyBudgetDetailDto>;

public sealed class GetUncertaintyBudgetByIdHandler(IAppDbContext db)
    : IQueryHandler<GetUncertaintyBudgetByIdQuery, UncertaintyBudgetDetailDto>
{
    public async Task<UncertaintyBudgetDetailDto> Handle(GetUncertaintyBudgetByIdQuery q, CancellationToken ct)
    {
        var b = await db.UncertaintyBudgets.AsNoTracking()
            .Include(x => x.Components)
            .FirstOrDefaultAsync(x => x.Id == q.BudgetId, ct)
            ?? throw new DomainException("MU-404", "Uncertainty budget not found.");

        return new UncertaintyBudgetDetailDto(
            b.Id, b.BudgetRef, b.Analyte, b.Method, b.Unit, b.Level,
            b.CoverageFactor, b.TargetExpandedUncertainty, b.Status.ToString(),
            b.CombinedStandardUncertainty, b.ExpandedUncertainty, b.MeetsTarget,
            b.ApprovedBy, b.ApprovedAtUtc,
            b.Components.Select(c => new UncertaintyComponentDto(
                c.Id, c.Name, c.Type.ToString(), c.RelativeStandardUncertainty, c.Source)).ToList());
    }
}
