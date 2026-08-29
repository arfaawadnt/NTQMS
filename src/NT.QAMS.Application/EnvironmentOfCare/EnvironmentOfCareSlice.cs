using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.EnvironmentOfCare;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.Domain.Improvement;
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

// ── M-22: environment-of-care → CAPA hand-off ────────────────────────────────────

/// <summary>The nonconformance <c>SourceRef</c> convention for a safety-round finding.</summary>
internal static class EnvironmentOfCareNcSource
{
    public static string RoundPrefix(string roundRef) => $"EOC:{roundRef}:";

    public static string FindingRef(string roundRef, Guid findingId) => $"EOC:{roundRef}:{findingId}";
}

/// <summary>
/// Raises a nonconformance / CAPA from a safety-round finding. This is a
/// <b>manual, suggested</b> hand-off (owner decision, 2026-08-30): the round
/// screen prompts to raise an NC for a significant finding, but a human decides;
/// once raised, the record follows the ordinary NC lifecycle. The finding's
/// severity seeds the initial risk assessment, refined during RCA. Creating a
/// CAPA is an NC.create act, so the actor needs that privilege. Idempotent: a
/// second call for the same finding returns the NC already raised.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.Nonconformances, PermissionAction.Create)]
public sealed record RaiseNcFromRoundFindingCommand(Guid RoundId, Guid FindingId) : ICommand<Guid>;

public sealed class RaiseNcFromRoundFindingHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs)
    : ICommandHandler<RaiseNcFromRoundFindingCommand, Guid>
{
    public async Task<Guid> Handle(RaiseNcFromRoundFindingCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var round = await StartRoundHandler.LoadRound(db, c.RoundId, ct);
        var finding = round.Findings.FirstOrDefault(f => f.Id == c.FindingId)
            ?? throw new DomainException("EOC-014", "Finding not found.");

        var sourceRef = EnvironmentOfCareNcSource.FindingRef(round.RoundRef, finding.Id);

        // Idempotency: a finding already handed off returns its existing NC.
        var existing = await db.Nonconformances.FirstOrDefaultAsync(n => n.SourceRef == sourceRef, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var (severity, likelihood) = SeverityToRisk(finding.Severity);
        var ncRef = await refs.NextAsync(tenantId, "NC", ct);
        var nc = Nonconformance.Raise(
            ncRef,
            $"Safety round {round.RoundRef} ({round.Area}): {finding.Description}",
            finding.Description,
            severity, likelihood,
            NcSourceType.EnvironmentOfCare, actor, sourceRef);
        nc.TenantId = tenantId;
        nc.BranchId = round.BranchId;
        nc.Submit();

        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync(ct);
        return nc.Id;
    }

    /// <summary>
    /// Seeds the CAPA's initial risk from the finding's severity so the corrective
    /// action inherits a proportionate priority (the RPN is refined during RCA).
    /// </summary>
    private static (int Severity, int Likelihood) SeverityToRisk(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Low => (2, 2),
        FindingSeverity.Medium => (3, 2),
        FindingSeverity.High => (4, 3),
        FindingSeverity.Critical => (5, 4),
        _ => (3, 3),
    };
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
        // M-10: server-side projection — the register needs two counts per
        // round, not every finding materialized (OpenFindingCount is an
        // EF-ignored domain property that used to force client evaluation).
        var query = db.SafetyRounds.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            query = query.Where(r => r.Type.ToString() == q.Type);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(r => r.Status.ToString() == q.Status);
        }

        return await query.OrderByDescending(r => r.ScheduledDate)
            .Select(r => new RoundListItemDto(
                r.Id, r.RoundRef, r.Area, r.Type.ToString(), r.ScheduledDate, r.Status.ToString(),
                r.Findings.Count(f => f.Status == FindingStatus.Open), r.Findings.Count))
            .ToListAsync(ct);
    }
}

public sealed record GetSafetyRoundByIdQuery(Guid RoundId) : IQuery<RoundDetailDto>;

public sealed class GetSafetyRoundByIdHandler(IAppDbContext db) : IQueryHandler<GetSafetyRoundByIdQuery, RoundDetailDto>
{
    public async Task<RoundDetailDto> Handle(GetSafetyRoundByIdQuery q, CancellationToken ct)
    {
        var r = await db.SafetyRounds.AsNoTracking().Include(x => x.Findings).SingleOrDefaultAsync(x => x.Id == q.RoundId, ct)
            ?? throw new DomainException("EOC-404", "Safety round not found.");

        // M-22: a finding may have been handed off to a nonconformance. Resolve the
        // origin links for this round in one query (keyed by the EOC source ref) so
        // the detail view can show which findings already carry a CAPA.
        var sourcePrefix = EnvironmentOfCareNcSource.RoundPrefix(r.RoundRef);
        var links = (await db.Nonconformances.AsNoTracking()
                .Where(n => n.SourceType == NcSourceType.EnvironmentOfCare
                    && n.SourceRef != null && n.SourceRef.StartsWith(sourcePrefix))
                .Select(n => new { n.SourceRef, n.NcRef })
                .ToListAsync(ct))
            .ToDictionary(x => x.SourceRef!, x => x.NcRef);

        return new RoundDetailDto(
            r.Id, r.RoundRef, r.Area, r.Type.ToString(), r.ScheduledDate, r.Status.ToString(),
            r.ConductedBy, r.CompletedAtUtc,
            r.Findings.Select(f => new RoundFindingDto(
                f.Id, f.Description, f.Severity.ToString(), f.Status.ToString(), f.CorrectiveNote, f.ResolvedAtUtc,
                links.GetValueOrDefault(EnvironmentOfCareNcSource.FindingRef(r.RoundRef, f.Id)))).ToList());
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
        // M-10: the dashboard is seven aggregates — computed in the database,
        // never by materializing every round, finding and drill.
        var scheduledRounds = await db.SafetyRounds.AsNoTracking()
            .CountAsync(r => r.Status == RoundStatus.Scheduled, ct);
        var completedRounds = await db.SafetyRounds.AsNoTracking()
            .CountAsync(r => r.Status == RoundStatus.Completed, ct);
        var openFindings = await db.SafetyRounds.AsNoTracking()
            .SelectMany(r => r.Findings)
            .CountAsync(f => f.Status == FindingStatus.Open, ct);
        var criticalOpenFindings = await db.SafetyRounds.AsNoTracking()
            .SelectMany(r => r.Findings)
            .CountAsync(f => f.Status == FindingStatus.Open && f.Severity == FindingSeverity.Critical, ct);
        var scheduledDrills = await db.Drills.AsNoTracking()
            .CountAsync(d => d.Status == DrillStatus.Scheduled, ct);
        var evaluated = await db.Drills.AsNoTracking()
            .Where(d => d.EvaluationScore != null)
            .Select(d => d.EvaluationScore!.Value)
            .ToListAsync(ct);

        return new EocSummaryDto(
            scheduledRounds, completedRounds, openFindings, criticalOpenFindings, scheduledDrills,
            evaluated.Count,
            evaluated.Count == 0 ? null : decimal.Round((decimal)evaluated.Average(), 1));
    }
}
