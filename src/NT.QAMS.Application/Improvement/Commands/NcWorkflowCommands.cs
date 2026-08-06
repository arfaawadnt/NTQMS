using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Compliance;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Improvement.Commands;

// â”€â”€ Raise â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record RaiseNcCommand(
    string Title, string Description, int Severity, int Likelihood, NcSourceType SourceType,
    Guid? BranchId = null, Guid? DepartmentId = null,
    QualityEventType EventType = QualityEventType.Nonconformity)
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
            command.Severity, command.Likelihood, command.SourceType, actor,
            eventType: command.EventType);

        nc.BranchId = command.BranchId;
        nc.DepartmentId = command.DepartmentId;
        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync(cancellationToken);
        return nc.Id;
    }
}

// â”€â”€ Workflow transitions (load â†’ guarded aggregate method â†’ save) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record SubmitNcCommand(Guid NcId) : ICommand;
[RequireInternalActor]
public sealed record TriageNcCommand(Guid NcId, Guid AssigneeId) : ICommand;
[RequireInternalActor]
public sealed record RejectNcCommand(Guid NcId, string Reason) : ICommand;
[RequireInternalActor]
public sealed record RecordRcaCommand(Guid NcId, RcaMethod Method, string Analysis) : ICommand;
[RequireInternalActor]
public sealed record PlanCapaActionCommand(
    Guid NcId, CapaActionType Type, string Details, Guid OwnerId, DateOnly DueDate) : ICommand<Guid>;
[RequireInternalActor]
public sealed record CompleteCapaActionCommand(Guid NcId, Guid ActionId) : ICommand;
[RequireInternalActor]
public sealed record SubmitNcForVerificationCommand(Guid NcId) : ICommand;
/// <summary>Verifying corrective-action effectiveness is a Part 11 signing ceremony: it requires the verifier's e-signature (account password + PIN).</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Nonconformances,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record VerifyNcCommand(Guid NcId, bool Passed, string Password, string Pin) : ICommand;
/// <summary>Confirming effectiveness (closing) is a Part 11 signing ceremony: it requires the actor's e-signature (account password + PIN).</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Nonconformances,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record ConfirmNcEffectivenessCommand(Guid NcId, bool Effective, string Password, string Pin) : ICommand;

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

public sealed class VerifyNcHandler(IAppDbContext db, ICurrentUser user, IESignatureService signatures)
    : ICommandHandler<VerifyNcCommand>
{
    public async Task Handle(VerifyNcCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var nc = await NcLoader.LoadAsync(db, c.NcId, ct);

        // Pre-validate every verification precondition BEFORE minting the signature —
        // the signature ledger is append-only, so a signature must never exist for a
        // verification that then fails its state or SoD gates (mirrors the publish
        // ceremony in DocumentCommands.cs). The aggregate re-checks both invariants.
        if (nc.Status != NcStatus.PendingVerification)
        {
            throw new InvalidStateTransitionException(
                "NC-021", $"Cannot verify a nonconformance in state {nc.Status}.");
        }

        if (actor == nc.RaisedBy)
        {
            throw new DomainException(
                "SOD-CAPA-002", "Segregation of duties: the raiser cannot verify their own nonconformance.");
        }

        // Bind the signature to the exact determination being attested (§11.70): the
        // nonconformance identity, its risk, the outcome, and the CAPA set that was verified.
        var contentHash = SignatureContentHash.Compute(
            ("nc", nc.NcRef),
            ("title", nc.Title),
            ("rpn", nc.Rpn.ToString(CultureInfo.InvariantCulture)),
            ("outcome", c.Passed ? "passed" : "not-passed"),
            ("capaActions", string.Join(",",
                nc.CapaActions.OrderBy(a => a.Id).Select(a => $"{a.Id:N}:{a.Status}"))));

        // Verify both signature components and mint the immutable signature, then apply
        // the (already pre-validated) verification.
        await signatures.SignAsync(
            actor, c.Password, c.Pin,
            $"Verified corrective-action effectiveness on {nc.NcRef}: {(c.Passed ? "passed" : "not passed")}",
            $"NC:{nc.Id:N}", contentHash, ct);

        nc.Verify(c.Passed, actor);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ConfirmNcEffectivenessHandler(
    IAppDbContext db, ICurrentUser user, IESignatureService signatures)
    : ICommandHandler<ConfirmNcEffectivenessCommand>
{
    public async Task Handle(ConfirmNcEffectivenessCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var nc = await NcLoader.LoadAsync(db, c.NcId, ct);

        // Pre-validate before minting (append-only ledger; mirrors the verify pilot / DocumentCommands).
        if (nc.Status != NcStatus.EffectivenessCheck)
        {
            throw new InvalidStateTransitionException(
                "NC-022", $"Cannot confirm effectiveness a nonconformance in state {nc.Status}.");
        }

        // SoD applies only to a closing (effective) determination (SOD-CAPA-001).
        if (c.Effective && actor == nc.RaisedBy)
        {
            throw new DomainException(
                "SOD-CAPA-001", "Segregation of duties: the raiser cannot close their own nonconformance.");
        }

        var subjectRef = $"NC:{nc.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin,
            $"Confirmed corrective-action effectiveness on {nc.NcRef}: {(c.Effective ? "effective (closed)" : "not effective")}",
            subjectRef,
            SignatureContentHash.Compute(
                ("nc", nc.NcRef), ("outcome", c.Effective ? "effective" : "not-effective")), ct);

        nc.ConfirmEffectiveness(c.Effective, actor);
        await db.SaveChangesAsync(ct);
    }
}
