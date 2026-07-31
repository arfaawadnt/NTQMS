using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Improvement;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Improvement;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record DefineQualityObjectiveCommand(
    string Title, string? Description, string Metric, string Unit,
    decimal TargetValue, string Direction, Guid OwnerId,
    DateOnly PeriodStart, DateOnly PeriodEnd,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class DefineQualityObjectiveValidator : AbstractValidator<DefineQualityObjectiveCommand>
{
    public DefineQualityObjectiveValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Metric).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Unit).MaximumLength(30);
        RuleFor(x => x.Direction).NotEmpty();
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}

public sealed class DefineQualityObjectiveHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<DefineQualityObjectiveCommand, Guid>
{
    public async Task<Guid> Handle(DefineQualityObjectiveCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var objectiveRef = await refs.NextAsync(tenantId, "QO", ct);
        var objective = QualityObjective.Define(
            objectiveRef, c.Title, c.Description, c.Metric, c.Unit, c.TargetValue,
            Enum.Parse<ObjectiveDirection>(c.Direction, ignoreCase: true),
            c.OwnerId, c.PeriodStart, c.PeriodEnd);
        objective.BranchId = c.BranchId;
        objective.DepartmentId = c.DepartmentId;
        db.QualityObjectives.Add(objective);
        await db.SaveChangesAsync(ct);
        return objective.Id;
    }
}

[RequireInternalActor]
public sealed record RecordObjectiveProgressCommand(
    Guid ObjectiveId, DateOnly MeasuredOn, decimal Value, string? Comment) : ICommand<Guid>;
[RequireInternalActor]
public sealed record CloseObjectiveCommand(Guid ObjectiveId, string Outcome, string Note) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class CloseObjectiveValidator : AbstractValidator<CloseObjectiveCommand>
{
    public CloseObjectiveValidator()
    {
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RecordObjectiveProgressValidator : AbstractValidator<RecordObjectiveProgressCommand>
{
    public RecordObjectiveProgressValidator()
    {
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public sealed class QualityObjectiveWorkflowHandlers(IAppDbContext db, ICurrentUser user) :
    ICommandHandler<RecordObjectiveProgressCommand, Guid>,
    ICommandHandler<CloseObjectiveCommand>
{
    public async Task<Guid> Handle(RecordObjectiveProgressCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var objective = await LoadAsync(c.ObjectiveId, ct);
        var id = objective.RecordProgress(c.MeasuredOn, c.Value, actor, c.Comment);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task Handle(CloseObjectiveCommand c, CancellationToken ct)
    {
        var objective = await LoadAsync(c.ObjectiveId, ct);
        switch (c.Outcome.ToLowerInvariant())
        {
            case "achieved": objective.CloseAsAchieved(c.Note); break;
            case "missed": objective.CloseAsMissed(c.Note); break;
            case "cancelled": objective.Cancel(c.Note); break;
            default: throw new DomainException("OBJ-014", "The outcome must be Achieved, Missed or Cancelled.");
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<QualityObjective> LoadAsync(Guid id, CancellationToken ct) =>
        await db.QualityObjectives.Include(o => o.Updates)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new DomainException("OBJ-404", "Quality objective not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetQualityObjectivesQuery(string? Status)
    : IQuery<IReadOnlyList<QualityObjectiveListItemDto>>;

public sealed class GetQualityObjectivesHandler(IAppDbContext db)
    : IQueryHandler<GetQualityObjectivesQuery, IReadOnlyList<QualityObjectiveListItemDto>>
{
    public async Task<IReadOnlyList<QualityObjectiveListItemDto>> Handle(
        GetQualityObjectivesQuery q, CancellationToken ct)
    {
        var objectives = db.QualityObjectives.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<ObjectiveStatus>(q.Status, ignoreCase: true, out var status))
        {
            objectives = objectives.Where(o => o.Status == status);
        }

        // CurrentValue/OnTarget are domain-computed â€” materialise then project.
        var rows = await objectives
            .Include(o => o.Updates)
            .OrderByDescending(o => o.PeriodStart)
            .ToListAsync(ct);

        return rows.Select(o => new QualityObjectiveListItemDto(
                o.Id, o.ObjectiveRef, o.Title, o.Metric, o.Unit, o.TargetValue, o.Direction.ToString(),
                o.OwnerId, o.PeriodStart, o.PeriodEnd, o.Status.ToString(), o.CurrentValue, o.OnTarget,
                o.BranchId, o.DepartmentId))
            .ToList();
    }
}

public sealed record GetQualityObjectiveByIdQuery(Guid ObjectiveId) : IQuery<QualityObjectiveDetailDto>;

public sealed class GetQualityObjectiveByIdHandler(IAppDbContext db)
    : IQueryHandler<GetQualityObjectiveByIdQuery, QualityObjectiveDetailDto>
{
    public async Task<QualityObjectiveDetailDto> Handle(GetQualityObjectiveByIdQuery q, CancellationToken ct)
    {
        var o = await db.QualityObjectives.AsNoTracking()
            .Include(x => x.Updates)
            .FirstOrDefaultAsync(x => x.Id == q.ObjectiveId, ct)
            ?? throw new DomainException("OBJ-404", "Quality objective not found.");

        return new QualityObjectiveDetailDto(
            o.Id, o.ObjectiveRef, o.Title, o.Description, o.Metric, o.Unit,
            o.TargetValue, o.Direction.ToString(), o.OwnerId, o.PeriodStart, o.PeriodEnd,
            o.Status.ToString(), o.CurrentValue, o.OnTarget, o.ClosureNote,
            o.BranchId, o.DepartmentId,
            o.Updates.OrderByDescending(u => u.MeasuredOn)
                .Select(u => new ObjectiveProgressDto(u.Id, u.MeasuredOn, u.Value, u.RecordedById, u.Comment))
                .ToList());
    }
}
