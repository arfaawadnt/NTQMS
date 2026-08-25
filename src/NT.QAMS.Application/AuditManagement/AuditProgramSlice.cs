using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AuditManagement;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AuditManagement;

// ── Commands ─────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Audits, PermissionAction.Create)]
public sealed record CreateAuditProgramCommand(int Year, string Title) : ICommand<Guid>;

public sealed class CreateAuditProgramValidator : AbstractValidator<CreateAuditProgramCommand>
{
    public CreateAuditProgramValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateAuditProgramHandler(IAppDbContext db) : ICommandHandler<CreateAuditProgramCommand, Guid>
{
    public async Task<Guid> Handle(CreateAuditProgramCommand c, CancellationToken ct)
    {
        var program = AuditProgram.Create(c.Year, c.Title);
        db.AuditPrograms.Add(program);
        await db.SaveChangesAsync(ct);
        return program.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Audits, PermissionAction.Create)]
public sealed record AddPlannedAuditCommand(
    Guid ProgramId, string ScopeArea, Guid? DepartmentId, string? StandardChapter,
    PlannedAuditPriority Priority, int PlannedQuarter) : ICommand<Guid>;

public sealed class AddPlannedAuditValidator : AbstractValidator<AddPlannedAuditCommand>
{
    public AddPlannedAuditValidator()
    {
        RuleFor(x => x.ScopeArea).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StandardChapter).MaximumLength(120);
        RuleFor(x => x.PlannedQuarter).InclusiveBetween(1, 4);
    }
}

public sealed class AddPlannedAuditHandler(IAppDbContext db) : ICommandHandler<AddPlannedAuditCommand, Guid>
{
    public async Task<Guid> Handle(AddPlannedAuditCommand c, CancellationToken ct)
    {
        var program = await Load(db, c.ProgramId, ct);
        var id = program.AddPlannedAudit(c.ScopeArea, c.DepartmentId, c.StandardChapter, c.Priority, c.PlannedQuarter);
        await db.SaveChangesAsync(ct);
        return id;
    }

    internal static async Task<AuditProgram> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.AuditPrograms.Include(p => p.Plan).SingleOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new DomainException("APG-404", "Audit programme not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Audits, PermissionAction.Approve)]
public sealed record ActivateAuditProgramCommand(Guid ProgramId) : ICommand;

public sealed class ActivateAuditProgramHandler(IAppDbContext db) : ICommandHandler<ActivateAuditProgramCommand>
{
    public async Task Handle(ActivateAuditProgramCommand c, CancellationToken ct)
    {
        (await AddPlannedAuditHandler.Load(db, c.ProgramId, ct)).Activate();
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Audits, PermissionAction.Approve)]
public sealed record LinkScheduledAuditCommand(Guid ProgramId, Guid PlannedAuditId, Guid AuditId) : ICommand;

public sealed class LinkScheduledAuditHandler(IAppDbContext db) : ICommandHandler<LinkScheduledAuditCommand>
{
    public async Task Handle(LinkScheduledAuditCommand c, CancellationToken ct)
    {
        // The audit must exist in this tenant (the global filter scopes the lookup).
        var auditExists = await db.Audits.AnyAsync(a => a.Id == c.AuditId, ct);
        if (!auditExists)
        {
            throw new DomainException("APG-019", "The referenced audit was not found.");
        }

        var program = await AddPlannedAuditHandler.Load(db, c.ProgramId, ct);
        program.LinkScheduledAudit(c.PlannedAuditId, c.AuditId);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Audits, PermissionAction.Approve)]
public sealed record CompletePlannedAuditCommand(Guid ProgramId, Guid PlannedAuditId, DateOnly CompletedOn) : ICommand;

public sealed class CompletePlannedAuditHandler(IAppDbContext db) : ICommandHandler<CompletePlannedAuditCommand>
{
    public async Task Handle(CompletePlannedAuditCommand c, CancellationToken ct)
    {
        var program = await AddPlannedAuditHandler.Load(db, c.ProgramId, ct);
        program.CompletePlannedAudit(c.PlannedAuditId, c.CompletedOn);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Audits, PermissionAction.Void)]
public sealed record CloseAuditProgramCommand(Guid ProgramId) : ICommand;

public sealed class CloseAuditProgramHandler(IAppDbContext db) : ICommandHandler<CloseAuditProgramCommand>
{
    public async Task Handle(CloseAuditProgramCommand c, CancellationToken ct)
    {
        (await AddPlannedAuditHandler.Load(db, c.ProgramId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetAuditProgramsQuery(string? Status = null) : IQuery<IReadOnlyList<AuditProgramListItemDto>>;

public sealed class GetAuditProgramsHandler(IAppDbContext db)
    : IQueryHandler<GetAuditProgramsQuery, IReadOnlyList<AuditProgramListItemDto>>
{
    public async Task<IReadOnlyList<AuditProgramListItemDto>> Handle(GetAuditProgramsQuery q, CancellationToken ct)
    {
        var query = db.AuditPrograms.AsNoTracking().Include(p => p.Plan).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(p => p.Status.ToString() == q.Status);
        }

        var programs = await query.OrderByDescending(p => p.Year).ToListAsync(ct);
        return programs
            .Select(p => new AuditProgramListItemDto(
                p.Id, p.Year, p.Title, p.Status.ToString(), p.Plan.Count, Coverage(p).CoveragePercent))
            .ToList();
    }

    internal static AuditProgramCoverageDto Coverage(AuditProgram p)
    {
        var total = p.Plan.Count;
        var completed = p.Plan.Count(l => l.Status == PlannedAuditStatus.Completed);
        var scheduledOrBeyond = p.Plan.Count(l => l.Status != PlannedAuditStatus.Planned);
        var scheduledOnly = p.Plan.Count(l => l.Status == PlannedAuditStatus.Scheduled);
        var coverage = total == 0 ? 0m : decimal.Round(completed * 100m / total, 1);
        var scheduledPct = total == 0 ? 0m : decimal.Round(scheduledOrBeyond * 100m / total, 1);
        return new AuditProgramCoverageDto(total, scheduledOnly, completed, coverage, scheduledPct);
    }
}

public sealed record GetAuditProgramByIdQuery(Guid ProgramId) : IQuery<AuditProgramDetailDto>;

public sealed class GetAuditProgramByIdHandler(IAppDbContext db)
    : IQueryHandler<GetAuditProgramByIdQuery, AuditProgramDetailDto>
{
    public async Task<AuditProgramDetailDto> Handle(GetAuditProgramByIdQuery q, CancellationToken ct)
    {
        var p = await db.AuditPrograms.AsNoTracking().Include(x => x.Plan)
            .SingleOrDefaultAsync(x => x.Id == q.ProgramId, ct)
            ?? throw new DomainException("APG-404", "Audit programme not found.");

        var plan = p.Plan
            .OrderBy(l => l.PlannedQuarter).ThenByDescending(l => l.Priority)
            .Select(l => new PlannedAuditDto(
                l.Id, l.ScopeArea, l.DepartmentId, l.StandardChapter, l.Priority.ToString(),
                l.PlannedQuarter, l.Status.ToString(), l.ScheduledAuditId, l.CompletedOn))
            .ToList();

        return new AuditProgramDetailDto(
            p.Id, p.Year, p.Title, p.Status.ToString(), GetAuditProgramsHandler.Coverage(p), plan);
    }
}
