using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.EnvironmentOfCare;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.EnvironmentOfCare;

// ── Safety-round commands ───────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Create)]
public sealed record ScheduleRoundCommand(string Area, RoundType Type, DateOnly ScheduledDate) : ICommand<Guid>;

public sealed class ScheduleRoundValidator : AbstractValidator<ScheduleRoundCommand>
{
    public ScheduleRoundValidator() => RuleFor(x => x.Area).NotEmpty().MaximumLength(150);
}

public sealed class ScheduleRoundHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ScheduleRoundCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleRoundCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var roundRef = await refs.NextAsync(tenantId, "EOR", ct);
        var round = SafetyRound.Schedule(roundRef, c.Area, c.Type, c.ScheduledDate);
        db.SafetyRounds.Add(round);
        await db.SaveChangesAsync(ct);
        return round.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
public sealed record StartRoundCommand(Guid RoundId) : ICommand;

public sealed class StartRoundHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<StartRoundCommand>
{
    public async Task Handle(StartRoundCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var round = await LoadRound(db, c.RoundId, ct);
        round.Start(actor);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<SafetyRound> LoadRound(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.SafetyRounds.Include(r => r.Findings).SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new DomainException("EOC-404", "Safety round not found.");
}

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
public sealed record AddFindingCommand(Guid RoundId, string Description, FindingSeverity Severity) : ICommand<Guid>;

public sealed class AddFindingValidator : AbstractValidator<AddFindingCommand>
{
    public AddFindingValidator() => RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
}

public sealed class AddFindingHandler(IAppDbContext db) : ICommandHandler<AddFindingCommand, Guid>
{
    public async Task<Guid> Handle(AddFindingCommand c, CancellationToken ct)
    {
        var round = await StartRoundHandler.LoadRound(db, c.RoundId, ct);
        var id = round.AddFinding(c.Description, c.Severity);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
public sealed record ResolveFindingCommand(Guid RoundId, Guid FindingId, string Note) : ICommand;

public sealed class ResolveFindingValidator : AbstractValidator<ResolveFindingCommand>
{
    public ResolveFindingValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
}

public sealed class ResolveFindingHandler(IAppDbContext db, IClock clock) : ICommandHandler<ResolveFindingCommand>
{
    public async Task Handle(ResolveFindingCommand c, CancellationToken ct)
    {
        var round = await StartRoundHandler.LoadRound(db, c.RoundId, ct);
        round.ResolveFinding(c.FindingId, c.Note, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Void)]
public sealed record CompleteRoundCommand(Guid RoundId) : ICommand;

public sealed class CompleteRoundHandler(IAppDbContext db) : ICommandHandler<CompleteRoundCommand>
{
    public async Task Handle(CompleteRoundCommand c, CancellationToken ct)
    {
        (await StartRoundHandler.LoadRound(db, c.RoundId, ct)).Complete();
        await db.SaveChangesAsync(ct);
    }
}

// ── Drill commands ──────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Create)]
public sealed record ScheduleDrillCommand(DrillType Type, string Location, DateOnly ScheduledDate) : ICommand<Guid>;

public sealed class ScheduleDrillValidator : AbstractValidator<ScheduleDrillCommand>
{
    public ScheduleDrillValidator() => RuleFor(x => x.Location).NotEmpty().MaximumLength(150);
}

public sealed class ScheduleDrillHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ScheduleDrillCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleDrillCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var drillRef = await refs.NextAsync(tenantId, "EOD", ct);
        var drill = Drill.Schedule(drillRef, c.Type, c.Location, c.ScheduledDate);
        db.Drills.Add(drill);
        await db.SaveChangesAsync(ct);
        return drill.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
public sealed record ExecuteDrillCommand(Guid DrillId, DateTimeOffset ExecutedAtUtc, int ParticipantCount) : ICommand;

public sealed class ExecuteDrillHandler(IAppDbContext db) : ICommandHandler<ExecuteDrillCommand>
{
    public async Task Handle(ExecuteDrillCommand c, CancellationToken ct)
    {
        var drill = await LoadDrill(db, c.DrillId, ct);
        drill.Execute(c.ExecutedAtUtc, c.ParticipantCount);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<Drill> LoadDrill(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Drills.SingleOrDefaultAsync(d => d.Id == id, ct)
        ?? throw new DomainException("DRL-404", "Drill not found.");
}

[RequirePermissionPolicy(PermissionCatalog.EnvironmentOfCare, PermissionAction.Approve)]
public sealed record EvaluateDrillCommand(Guid DrillId, int Score, string ImprovementNotes) : ICommand;

public sealed class EvaluateDrillHandler(IAppDbContext db) : ICommandHandler<EvaluateDrillCommand>
{
    public async Task Handle(EvaluateDrillCommand c, CancellationToken ct)
    {
        (await ExecuteDrillHandler.LoadDrill(db, c.DrillId, ct)).Evaluate(c.Score, c.ImprovementNotes);
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetSafetyRoundsQuery(string? Type = null, string? Status = null)
    : IQuery<IReadOnlyList<RoundListItemDto>>;

public sealed class GetSafetyRoundsHandler(IAppDbContext db)
    : IQueryHandler<GetSafetyRoundsQuery, IReadOnlyList<RoundListItemDto>>
{
    public async Task<IReadOnlyList<RoundListItemDto>> Handle(GetSafetyRoundsQuery q, CancellationToken ct)
    {
        var query = db.SafetyRounds.AsNoTracking().Include(r => r.Findings).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            query = query.Where(r => r.Type.ToString() == q.Type);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(r => r.Status.ToString() == q.Status);
        }

        var rounds = await query.OrderByDescending(r => r.ScheduledDate).ToListAsync(ct);
        return rounds.Select(r => new RoundListItemDto(
            r.Id, r.RoundRef, r.Area, r.Type.ToString(), r.ScheduledDate, r.Status.ToString(),
            r.OpenFindingCount, r.Findings.Count)).ToList();
    }
}

public sealed record GetSafetyRoundByIdQuery(Guid RoundId) : IQuery<RoundDetailDto>;

public sealed class GetSafetyRoundByIdHandler(IAppDbContext db) : IQueryHandler<GetSafetyRoundByIdQuery, RoundDetailDto>
{
    public async Task<RoundDetailDto> Handle(GetSafetyRoundByIdQuery q, CancellationToken ct)
    {
        var r = await db.SafetyRounds.AsNoTracking().Include(x => x.Findings).SingleOrDefaultAsync(x => x.Id == q.RoundId, ct)
            ?? throw new DomainException("EOC-404", "Safety round not found.");

        return new RoundDetailDto(
            r.Id, r.RoundRef, r.Area, r.Type.ToString(), r.ScheduledDate, r.Status.ToString(),
            r.ConductedBy, r.CompletedAtUtc,
            r.Findings.Select(f => new RoundFindingDto(
                f.Id, f.Description, f.Severity.ToString(), f.Status.ToString(), f.CorrectiveNote, f.ResolvedAtUtc)).ToList());
    }
}

public sealed record GetDrillsQuery(string? Type = null, string? Status = null) : IQuery<IReadOnlyList<DrillListItemDto>>;

public sealed class GetDrillsHandler(IAppDbContext db) : IQueryHandler<GetDrillsQuery, IReadOnlyList<DrillListItemDto>>
{
    public async Task<IReadOnlyList<DrillListItemDto>> Handle(GetDrillsQuery q, CancellationToken ct)
    {
        var query = db.Drills.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            query = query.Where(d => d.Type.ToString() == q.Type);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(d => d.Status.ToString() == q.Status);
        }

        var drills = await query.OrderByDescending(d => d.ScheduledDate).ToListAsync(ct);
        return drills.Select(d => new DrillListItemDto(
            d.Id, d.DrillRef, d.Type.ToString(), d.Location, d.ScheduledDate, d.Status.ToString(),
            d.ParticipantCount, d.EvaluationScore, d.Effectiveness)).ToList();
    }
}

public sealed record GetDrillByIdQuery(Guid DrillId) : IQuery<DrillDetailDto>;

public sealed class GetDrillByIdHandler(IAppDbContext db) : IQueryHandler<GetDrillByIdQuery, DrillDetailDto>
{
    public async Task<DrillDetailDto> Handle(GetDrillByIdQuery q, CancellationToken ct)
    {
        var d = await db.Drills.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.DrillId, ct)
            ?? throw new DomainException("DRL-404", "Drill not found.");

        return new DrillDetailDto(
            d.Id, d.DrillRef, d.Type.ToString(), d.Location, d.ScheduledDate, d.Status.ToString(),
            d.ExecutedAtUtc, d.ParticipantCount, d.EvaluationScore, d.Effectiveness, d.ImprovementNotes);
    }
}

/// <summary>
/// The environment-of-care dashboard (HQMS M15): round completion, the open-findings backlog
/// (with the critical subset), and drill coverage and mean effectiveness score.
/// </summary>
public sealed record GetEocSummaryQuery : IQuery<EocSummaryDto>;

public sealed class GetEocSummaryHandler(IAppDbContext db) : IQueryHandler<GetEocSummaryQuery, EocSummaryDto>
{
    public async Task<EocSummaryDto> Handle(GetEocSummaryQuery q, CancellationToken ct)
    {
        var rounds = await db.SafetyRounds.AsNoTracking().Include(r => r.Findings).ToListAsync(ct);
        var drills = await db.Drills.AsNoTracking().ToListAsync(ct);

        var openFindings = rounds.SelectMany(r => r.Findings).Where(f => f.Status == FindingStatus.Open).ToList();
        var evaluated = drills.Where(d => d.EvaluationScore.HasValue).Select(d => d.EvaluationScore!.Value).ToList();

        return new EocSummaryDto(
            rounds.Count(r => r.Status == RoundStatus.Scheduled),
            rounds.Count(r => r.Status == RoundStatus.Completed),
            openFindings.Count,
            openFindings.Count(f => f.Severity == FindingSeverity.Critical),
            drills.Count(d => d.Status == DrillStatus.Scheduled),
            evaluated.Count,
            evaluated.Count == 0 ? null : decimal.Round((decimal)evaluated.Average(), 1));
    }
}
