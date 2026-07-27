using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AuditManagement.Commands;

[RequireInternalActor]
public sealed record ScheduleAuditCommand(
    string Title, AuditType Type, Guid LeadAuditorId, DateOnly PlannedDate,
    IReadOnlyList<(string IsoClause, string Question)> Checklist,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class ScheduleAuditValidator : AbstractValidator<ScheduleAuditCommand>
{
    public ScheduleAuditValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.LeadAuditorId).NotEmpty();
        RuleFor(x => x.Checklist).NotEmpty()
            .WithMessage("An audit needs at least one checklist item.");
    }
}

public sealed class ScheduleAuditHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ScheduleAuditCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleAuditCommand command, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        var auditRef = await refs.NextAsync(tenantId, "AUD", ct);
        var audit = Audit.Schedule(
            auditRef, command.Title, command.Type, command.LeadAuditorId, command.PlannedDate);

        foreach (var (clause, question) in command.Checklist)
        {
            audit.AddChecklistItem(clause, question);
        }

        audit.BranchId = command.BranchId;
        audit.DepartmentId = command.DepartmentId;
        db.Audits.Add(audit);
        await db.SaveChangesAsync(ct);
        return audit.Id;
    }
}

[RequireInternalActor]
public sealed record StartAuditCommand(Guid AuditId) : ICommand;
[RequireInternalActor]
public sealed record AnswerChecklistItemCommand(
    Guid AuditId, Guid ItemId, ChecklistVerdict Verdict, string? Evidence) : ICommand;
[RequireInternalActor]
public sealed record RaiseFindingCommand(Guid AuditId, FindingGrade Grade, string Description)
    : ICommand<Guid>;
[RequireInternalActor]
public sealed record SignOffAuditCommand(Guid AuditId) : ICommand;

public sealed class RaiseFindingValidator : AbstractValidator<RaiseFindingCommand>
{
    public RaiseFindingValidator() => RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
}

internal static class AuditLoader
{
    public static async Task<Audit> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Audits
            .Include(a => a.Checklist)
            .Include(a => a.Findings)
            .SingleOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new DomainException("AUD-404", "Audit not found.");
}

public sealed class StartAuditHandler(IAppDbContext db) : ICommandHandler<StartAuditCommand>
{
    public async Task Handle(StartAuditCommand c, CancellationToken ct)
    {
        (await AuditLoader.LoadAsync(db, c.AuditId, ct)).Start();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class AnswerChecklistItemHandler(IAppDbContext db)
    : ICommandHandler<AnswerChecklistItemCommand>
{
    public async Task Handle(AnswerChecklistItemCommand c, CancellationToken ct)
    {
        (await AuditLoader.LoadAsync(db, c.AuditId, ct))
            .AnswerChecklistItem(c.ItemId, c.Verdict, c.Evidence);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RaiseFindingHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<RaiseFindingCommand, Guid>
{
    public async Task<Guid> Handle(RaiseFindingCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var audit = await AuditLoader.LoadAsync(db, c.AuditId, ct);
        var findingId = audit.RaiseFinding(c.Grade, c.Description, actor);
        await db.SaveChangesAsync(ct);
        return findingId;
    }
}

public sealed class SignOffAuditHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<SignOffAuditCommand>
{
    public async Task Handle(SignOffAuditCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        (await AuditLoader.LoadAsync(db, c.AuditId, ct)).SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}
