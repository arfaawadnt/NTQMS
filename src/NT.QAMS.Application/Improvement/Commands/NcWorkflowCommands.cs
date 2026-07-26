using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Improvement.Commands;

// ── Raise ────────────────────────────────────────────────────────────────────

public sealed record RaiseNcCommand(
    string Title, string Description, int Severity, int Likelihood, NcSourceType SourceType,
    Guid? BranchId = null, Guid? DepartmentId = null)
    : ICommand<Guid>;

public sealed class RaiseNcValidator : AbstractValidator<RaiseNcCommand>
{
    public RaiseNcValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Severity).InclusiveBetween(1, 5);
        RuleFor(x => x.Likelihood).InclusiveBetween(1, 5);
    }
}

public sealed class RaiseNcHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs)
    : ICommandHandler<RaiseNcCommand, Guid>
{
    public async Task<Guid> Handle(RaiseNcCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var ncRef = await refs.NextAsync(tenantId, "NC", cancellationToken);

        var nc = Nonconformance.Raise(
            ncRef, command.Title, command.Description,
            command.Severity, command.Likelihood, command.SourceType, actor);

        nc.BranchId = command.BranchId;
        nc.DepartmentId = command.DepartmentId;
        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync(cancellationToken);
        return nc.Id;
    }
}

// ── Workflow transitions (load → guarded aggregate method → save) ───────────

public sealed record SubmitNcCommand(Guid NcId) : ICommand;
public sealed record TriageNcCommand(Guid NcId, Guid AssigneeId) : ICommand;
public sealed record RejectNcCommand(Guid NcId, string Reason) : ICommand;
public sealed record RecordRcaCommand(Guid NcId, RcaMethod Method, string Analysis) : ICommand;
public sealed record PlanCapaActionCommand(
    Guid NcId, CapaActionType Type, string Details, Guid OwnerId, DateOnly DueDate) : ICommand<Guid>;
public sealed record CompleteCapaActionCommand(Guid NcId, Guid ActionId) : ICommand;
public sealed record SubmitNcForVerificationCommand(Guid NcId) : ICommand;
public sealed record VerifyNcCommand(Guid NcId, bool Passed) : ICommand;
public sealed record ConfirmNcEffectivenessCommand(Guid NcId, bool Effective) : ICommand;

public sealed class RejectNcValidator : AbstractValidator<RejectNcCommand>
{
    public RejectNcValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public sealed class RecordRcaValidator : AbstractValidator<RecordRcaCommand>
{
    public RecordRcaValidator() => RuleFor(x => x.Analysis).NotEmpty().MaximumLength(8000);
}

public sealed class PlanCapaActionValidator : AbstractValidator<PlanCapaActionCommand>
{
    public PlanCapaActionValidator()
    {
        RuleFor(x => x.Details).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}

/// <summary>Shared load-or-throw for all NC transition handlers (tenant filter applies).</summary>
internal static class NcLoader
{
    public static async Task<Nonconformance> LoadAsync(
        IAppDbContext db, Guid ncId, CancellationToken ct) =>
        await db.Nonconformances
            .Include(n => n.CapaActions)
            .Include(n => n.RcaRecords)
            .SingleOrDefaultAsync(n => n.Id == ncId, ct)
        ?? throw new DomainException("NC-404", "Nonconformance not found.");
}

public sealed class SubmitNcHandler(IAppDbContext db) : ICommandHandler<SubmitNcCommand>
{
    public async Task Handle(SubmitNcCommand c, CancellationToken ct)
    {
        (await NcLoader.LoadAsync(db, c.NcId, ct)).Submit();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class TriageNcHandler(IAppDbContext db) : ICommandHandler<TriageNcCommand>
{
    public async Task Handle(TriageNcCommand c, CancellationToken ct)
    {
        (await NcLoader.LoadAsync(db, c.NcId, ct)).Triage(c.AssigneeId);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RejectNcHandler(IAppDbContext db) : ICommandHandler<RejectNcCommand>
{
    public async Task Handle(RejectNcCommand c, CancellationToken ct)
    {
        (await NcLoader.LoadAsync(db, c.NcId, ct)).Reject(c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RecordRcaHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<RecordRcaCommand>
{
    public async Task Handle(RecordRcaCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        (await NcLoader.LoadAsync(db, c.NcId, ct)).RecordRca(c.Method, c.Analysis, actor);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class PlanCapaActionHandler(IAppDbContext db)
    : ICommandHandler<PlanCapaActionCommand, Guid>
{
    public async Task<Guid> Handle(PlanCapaActionCommand c, CancellationToken ct)
    {
        var nc = await NcLoader.LoadAsync(db, c.NcId, ct);
        var actionId = nc.PlanCapaAction(c.Type, c.Details, c.OwnerId, c.DueDate);
        await db.SaveChangesAsync(ct);
        return actionId;
    }
}

public sealed class CompleteCapaActionHandler(IAppDbContext db, IClock clock)
    : ICommandHandler<CompleteCapaActionCommand>
{
    public async Task Handle(CompleteCapaActionCommand c, CancellationToken ct)
    {
        (await NcLoader.LoadAsync(db, c.NcId, ct)).CompleteCapaAction(c.ActionId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SubmitNcForVerificationHandler(IAppDbContext db)
    : ICommandHandler<SubmitNcForVerificationCommand>
{
    public async Task Handle(SubmitNcForVerificationCommand c, CancellationToken ct)
    {
        (await NcLoader.LoadAsync(db, c.NcId, ct)).SubmitForVerification();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class VerifyNcHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<VerifyNcCommand>
{
    public async Task Handle(VerifyNcCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        (await NcLoader.LoadAsync(db, c.NcId, ct)).Verify(c.Passed, actor);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ConfirmNcEffectivenessHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<ConfirmNcEffectivenessCommand>
{
    public async Task Handle(ConfirmNcEffectivenessCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        (await NcLoader.LoadAsync(db, c.NcId, ct)).ConfirmEffectiveness(c.Effective, actor);
        await db.SaveChangesAsync(ct);
    }
}
