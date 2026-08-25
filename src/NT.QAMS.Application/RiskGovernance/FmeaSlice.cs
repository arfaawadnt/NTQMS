using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.RiskGovernance;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.RiskGovernance;

// ── Commands ─────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Risks, PermissionAction.Create)]
public sealed record CreateFmeaCommand(
    string Title, string ProcessName, FmeaType Type, Guid? BranchId, Guid? DepartmentId) : ICommand<Guid>;

public sealed class CreateFmeaValidator : AbstractValidator<CreateFmeaCommand>
{
    public CreateFmeaValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProcessName).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateFmeaHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<CreateFmeaCommand, Guid>
{
    public async Task<Guid> Handle(CreateFmeaCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var fmeaRef = await refs.NextAsync(tenantId, "FMEA", ct);
        var fmea = FmeaStudy.Create(fmeaRef, c.Title, c.ProcessName, c.Type);
        fmea.BranchId = c.BranchId;
        fmea.DepartmentId = c.DepartmentId;
        db.FmeaStudies.Add(fmea);
        await db.SaveChangesAsync(ct);
        return fmea.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Risks, PermissionAction.Create)]
public sealed record AddFailureModeCommand(
    Guid FmeaId, string ProcessStep, string FailureMode, string Effect, string Cause,
    int Severity, int Occurrence, int Detection) : ICommand<Guid>;

public sealed class AddFailureModeValidator : AbstractValidator<AddFailureModeCommand>
{
    public AddFailureModeValidator()
    {
        RuleFor(x => x.ProcessStep).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FailureMode).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Effect).MaximumLength(1000);
        RuleFor(x => x.Cause).MaximumLength(1000);
        RuleFor(x => x.Severity).InclusiveBetween(1, 10);
        RuleFor(x => x.Occurrence).InclusiveBetween(1, 10);
        RuleFor(x => x.Detection).InclusiveBetween(1, 10);
    }
}

public sealed class AddFailureModeHandler(IAppDbContext db) : ICommandHandler<AddFailureModeCommand, Guid>
{
    public async Task<Guid> Handle(AddFailureModeCommand c, CancellationToken ct)
    {
        var fmea = await Load(db, c.FmeaId, ct);
        var id = fmea.AddFailureMode(c.ProcessStep, c.FailureMode, c.Effect, c.Cause, c.Severity, c.Occurrence, c.Detection);
        await db.SaveChangesAsync(ct);
        return id;
    }

    internal static async Task<FmeaStudy> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.FmeaStudies.Include(f => f.FailureModes).SingleOrDefaultAsync(f => f.Id == id, ct)
        ?? throw new DomainException("FME-404", "FMEA not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Risks, PermissionAction.Edit)]
public sealed record RecommendActionCommand(Guid FmeaId, Guid FailureModeId, string Action, Guid? OwnerId) : ICommand;

public sealed class RecommendActionValidator : AbstractValidator<RecommendActionCommand>
{
    public RecommendActionValidator() => RuleFor(x => x.Action).NotEmpty().MaximumLength(2000);
}

public sealed class RecommendActionHandler(IAppDbContext db) : ICommandHandler<RecommendActionCommand>
{
    public async Task Handle(RecommendActionCommand c, CancellationToken ct)
    {
        var fmea = await AddFailureModeHandler.Load(db, c.FmeaId, ct);
        fmea.RecommendAction(c.FailureModeId, c.Action, c.OwnerId);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Risks, PermissionAction.Edit)]
public sealed record RecordFmeaResidualCommand(
    Guid FmeaId, Guid FailureModeId, int Severity, int Occurrence, int Detection) : ICommand;

public sealed class RecordFmeaResidualValidator : AbstractValidator<RecordFmeaResidualCommand>
{
    public RecordFmeaResidualValidator()
    {
        RuleFor(x => x.Severity).InclusiveBetween(1, 10);
        RuleFor(x => x.Occurrence).InclusiveBetween(1, 10);
        RuleFor(x => x.Detection).InclusiveBetween(1, 10);
    }
}

public sealed class RecordFmeaResidualHandler(IAppDbContext db) : ICommandHandler<RecordFmeaResidualCommand>
{
    public async Task Handle(RecordFmeaResidualCommand c, CancellationToken ct)
    {
        var fmea = await AddFailureModeHandler.Load(db, c.FmeaId, ct);
        fmea.RecordResidual(c.FailureModeId, c.Severity, c.Occurrence, c.Detection);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Risks, PermissionAction.Approve)]
public sealed record ActivateFmeaCommand(Guid FmeaId) : ICommand;

public sealed class ActivateFmeaHandler(IAppDbContext db) : ICommandHandler<ActivateFmeaCommand>
{
    public async Task Handle(ActivateFmeaCommand c, CancellationToken ct)
    {
        (await AddFailureModeHandler.Load(db, c.FmeaId, ct)).Activate();
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Risks, PermissionAction.Void)]
public sealed record CloseFmeaCommand(Guid FmeaId) : ICommand;

public sealed class CloseFmeaHandler(IAppDbContext db) : ICommandHandler<CloseFmeaCommand>
{
    public async Task Handle(CloseFmeaCommand c, CancellationToken ct)
    {
        (await AddFailureModeHandler.Load(db, c.FmeaId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetFmeasQuery(string? Status = null) : IQuery<IReadOnlyList<FmeaListItemDto>>;

public sealed class GetFmeasHandler(IAppDbContext db) : IQueryHandler<GetFmeasQuery, IReadOnlyList<FmeaListItemDto>>
{
    public async Task<IReadOnlyList<FmeaListItemDto>> Handle(GetFmeasQuery q, CancellationToken ct)
    {
        var query = db.FmeaStudies.AsNoTracking().Include(f => f.FailureModes).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(f => f.Status.ToString() == q.Status);
        }

        var studies = await query.OrderByDescending(f => f.CreatedAtUtc).ToListAsync(ct);
        return studies
            .Select(f => new FmeaListItemDto(
                f.Id, f.FmeaRef, f.Title, f.ProcessName, f.Type.ToString(), f.Status.ToString(),
                f.FailureModes.Count,
                f.FailureModes.Count(m => (m.ResidualRpn ?? m.Rpn) >= FmeaStudy.HighRpnThreshold),
                f.FailureModes.Count == 0 ? 0 : f.FailureModes.Max(m => m.Rpn)))
            .ToList();
    }
}

public sealed record GetFmeaByIdQuery(Guid FmeaId) : IQuery<FmeaDetailDto>;

public sealed class GetFmeaByIdHandler(IAppDbContext db) : IQueryHandler<GetFmeaByIdQuery, FmeaDetailDto>
{
    public async Task<FmeaDetailDto> Handle(GetFmeaByIdQuery q, CancellationToken ct)
    {
        var f = await db.FmeaStudies.AsNoTracking().Include(x => x.FailureModes)
            .SingleOrDefaultAsync(x => x.Id == q.FmeaId, ct)
            ?? throw new DomainException("FME-404", "FMEA not found.");

        // Highest RPN first — the worksheet is worked in priority order.
        var modes = f.FailureModes
            .OrderByDescending(m => m.Rpn)
            .Select(m => new FailureModeDto(
                m.Id, m.ProcessStep, m.FailureModeText, m.Effect, m.Cause,
                m.Severity, m.Occurrence, m.Detection, m.Rpn,
                m.RecommendedAction, m.ActionOwnerId,
                m.ResidualSeverity, m.ResidualOccurrence, m.ResidualDetection, m.ResidualRpn,
                m.Status.ToString()))
            .ToList();

        return new FmeaDetailDto(
            f.Id, f.FmeaRef, f.Title, f.ProcessName, f.Type.ToString(), f.Status.ToString(),
            f.BranchId, f.DepartmentId, FmeaStudy.HighRpnThreshold, modes);
    }
}
