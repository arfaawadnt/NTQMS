using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Compliance;
using NT.QAMS.Application.RiskGovernance;
using NT.QAMS.Contracts.Governance;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.SupplierQuality;

[RequireInternalActor]
public sealed record RegisterSupplierCommand(string Name, string SupplierType,
    bool IsOutsourcedClinicalService = false, string? ServiceScope = null,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class RegisterSupplierValidator : AbstractValidator<RegisterSupplierCommand>
{
    public RegisterSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ServiceScope).MaximumLength(300);
    }
}

public sealed class RegisterSupplierHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs)
    : ICommandHandler<RegisterSupplierCommand, Guid>
{
    public async Task<Guid> Handle(RegisterSupplierCommand c, CancellationToken ct)
    {
        var supplierRef = await refs.NextAsync(GovernanceHelpers.RequireTenant(tenant), "SUP", ct);
        var supplier = Supplier.Register(
            supplierRef, c.Name, c.SupplierType, GovernanceHelpers.RequireActor(user),
            c.IsOutsourcedClinicalService, c.ServiceScope);
        supplier.BranchId = c.BranchId;
        supplier.DepartmentId = c.DepartmentId;
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return supplier.Id;
    }
}

[RequireInternalActor]
public sealed record AddCertificateCommand(
    Guid SupplierId, string CertificateType, DateOnly ExpiresAt, Guid? FileId) : ICommand<Guid>;
/// <summary>Approving a supplier is a Part 11 signing ceremony: it requires the approver's e-signature (account password + PIN).</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Suppliers,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record ApproveSupplierCommand(Guid SupplierId, string Password, string Pin) : ICommand;
[RequireInternalActor]
public sealed record SuspendSupplierCommand(Guid SupplierId, string Reason) : ICommand;
[RequireInternalActor]
public sealed record RecordEvaluationCommand(
    Guid SupplierId, DateOnly PeriodStart, DateOnly PeriodEnd,
    IReadOnlyList<(string Criterion, decimal Weight, decimal Score)> Criteria) : ICommand<Guid>;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class RecordEvaluationValidator : AbstractValidator<RecordEvaluationCommand>
{
    public RecordEvaluationValidator()
    {
        // Bounds the serialized jsonb document indirectly: at most 50 criteria,
        // each name <= 200 chars.
        RuleFor(x => x.Criteria).NotEmpty().Must(c => c.Count <= 50)
            .WithMessage("An evaluation may carry at most 50 criteria.");
        RuleForEach(x => x.Criteria)
            .Must(c => !string.IsNullOrWhiteSpace(c.Criterion) && c.Criterion.Length <= 200)
            .WithMessage("Each criterion name is required and may not exceed 200 characters.");
    }
}

internal static class SupplierLoader
{
    public static async Task<Supplier> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Suppliers.Include(s => s.Certificates).Include(s => s.Contracts).Include(s => s.Cars)
            .SingleOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new DomainException("SUP-404", "Supplier not found.");
}

public sealed class AddCertificateHandler(IAppDbContext db) : ICommandHandler<AddCertificateCommand, Guid>
{
    public async Task<Guid> Handle(AddCertificateCommand c, CancellationToken ct)
    {
        if (c.FileId is { } fileId && !await db.Files.AnyAsync(f => f.Id == fileId, ct))
        {
            throw new DomainException("FILE-404", "Certificate file not found.");
        }

        var supplier = await SupplierLoader.LoadAsync(db, c.SupplierId, ct);
        var id = supplier.AddCertificate(c.CertificateType, c.ExpiresAt, c.FileId);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

public sealed class ApproveSupplierHandler(IAppDbContext db, ICurrentUser user, IESignatureService signatures)
    : ICommandHandler<ApproveSupplierCommand>
{
    public async Task Handle(ApproveSupplierCommand c, CancellationToken ct)
    {
        var actor = GovernanceHelpers.RequireActor(user);
        var supplier = await SupplierLoader.LoadAsync(db, c.SupplierId, ct);

        // Pre-validate before minting (append-only ledger; mirrors the pilot). The aggregate re-checks both.
        if (supplier.Status == SupplierStatus.Approved)
        {
            throw new InvalidStateTransitionException("SUP-010", "Supplier is already approved.");
        }

        if (actor == supplier.RegisteredBy)
        {
            throw new DomainException(
                "SOD-SUP-001", "Segregation of duties: the registrant cannot approve their own supplier.");
        }

        var subjectRef = $"SUP:{supplier.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, $"Approved supplier {supplier.SupplierRef}", subjectRef,
            SignatureContentHash.Compute(("supplier", supplier.SupplierRef), ("outcome", "approved")), ct);

        supplier.Approve(actor);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SuspendSupplierHandler(IAppDbContext db) : ICommandHandler<SuspendSupplierCommand>
{
    public async Task Handle(SuspendSupplierCommand c, CancellationToken ct)
    {
        (await SupplierLoader.LoadAsync(db, c.SupplierId, ct)).Suspend(c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RecordEvaluationHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<RecordEvaluationCommand, Guid>
{
    public async Task<Guid> Handle(RecordEvaluationCommand c, CancellationToken ct)
    {
        _ = await SupplierLoader.LoadAsync(db, c.SupplierId, ct); // exists + tenant-scoped
        var evaluation = SupplierEvaluation.Record(
            c.SupplierId, c.PeriodStart, c.PeriodEnd, c.Criteria, GovernanceHelpers.RequireActor(user));
        db.SupplierEvaluations.Add(evaluation);
        await db.SaveChangesAsync(ct);
        return evaluation.Id;
    }
}

// ── Contract / SLA register (HQMS M16) ──────────────────────────────────────────

[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Suppliers,
    NT.QAMS.Domain.Authorization.PermissionAction.Edit)]
public sealed record AddContractCommand(
    Guid SupplierId, string Title, DateOnly StartDate, DateOnly EndDate, string? SlaSummary) : ICommand<Guid>;

public sealed class AddContractValidator : AbstractValidator<AddContractCommand>
{
    public AddContractValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.SlaSummary).MaximumLength(4000);
    }
}

public sealed class AddContractHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<AddContractCommand, Guid>
{
    public async Task<Guid> Handle(AddContractCommand c, CancellationToken ct)
    {
        var supplier = await SupplierLoader.LoadAsync(db, c.SupplierId, ct);
        var contractRef = await refs.NextAsync(GovernanceHelpers.RequireTenant(tenant), "SCT", ct);
        var id = supplier.AddContract(contractRef, c.Title, c.StartDate, c.EndDate, c.SlaSummary);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Suppliers,
    NT.QAMS.Domain.Authorization.PermissionAction.Void)]
public sealed record TerminateContractCommand(Guid SupplierId, Guid ContractId, string Reason) : ICommand;

public sealed class TerminateContractValidator : AbstractValidator<TerminateContractCommand>
{
    public TerminateContractValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public sealed class TerminateContractHandler(IAppDbContext db) : ICommandHandler<TerminateContractCommand>
{
    public async Task Handle(TerminateContractCommand c, CancellationToken ct)
    {
        (await SupplierLoader.LoadAsync(db, c.SupplierId, ct)).TerminateContract(c.ContractId, c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

// ── Corrective-action requests (HQMS M16) ────────────────────────────────────────

[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Suppliers,
    NT.QAMS.Domain.Authorization.PermissionAction.Edit)]
public sealed record RaiseSupplierCarCommand(Guid SupplierId, string Description, DateOnly RaisedOn, DateOnly? DueDate)
    : ICommand<Guid>;

public sealed class RaiseSupplierCarValidator : AbstractValidator<RaiseSupplierCarCommand>
{
    public RaiseSupplierCarValidator() => RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
}

public sealed class RaiseSupplierCarHandler(IAppDbContext db) : ICommandHandler<RaiseSupplierCarCommand, Guid>
{
    public async Task<Guid> Handle(RaiseSupplierCarCommand c, CancellationToken ct)
    {
        var supplier = await SupplierLoader.LoadAsync(db, c.SupplierId, ct);
        var id = supplier.RaiseCar(c.Description, c.RaisedOn, c.DueDate);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Suppliers,
    NT.QAMS.Domain.Authorization.PermissionAction.Edit)]
public sealed record RecordCarResponseCommand(Guid SupplierId, Guid CarId, string Note, DateOnly On) : ICommand;

public sealed class RecordCarResponseValidator : AbstractValidator<RecordCarResponseCommand>
{
    public RecordCarResponseValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(4000);
}

public sealed class RecordCarResponseHandler(IAppDbContext db) : ICommandHandler<RecordCarResponseCommand>
{
    public async Task Handle(RecordCarResponseCommand c, CancellationToken ct)
    {
        (await SupplierLoader.LoadAsync(db, c.SupplierId, ct)).RecordCarResponse(c.CarId, c.Note, c.On);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Suppliers,
    NT.QAMS.Domain.Authorization.PermissionAction.Approve)]
public sealed record CloseSupplierCarCommand(Guid SupplierId, Guid CarId, bool Effective, string ClosureNote) : ICommand;

public sealed class CloseSupplierCarValidator : AbstractValidator<CloseSupplierCarCommand>
{
    public CloseSupplierCarValidator() => RuleFor(x => x.ClosureNote).NotEmpty().MaximumLength(4000);
}

public sealed class CloseSupplierCarHandler(IAppDbContext db) : ICommandHandler<CloseSupplierCarCommand>
{
    public async Task Handle(CloseSupplierCarCommand c, CancellationToken ct)
    {
        (await SupplierLoader.LoadAsync(db, c.SupplierId, ct)).CloseCar(c.CarId, c.Effective, c.ClosureNote);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetSuppliersQuery(
    string? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<SupplierListItemDto>>;

public sealed class GetSuppliersHandler(IAppDbContext db)
    : IQueryHandler<GetSuppliersQuery, Contracts.Common.PagedResponse<SupplierListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<SupplierListItemDto>> Handle(GetSuppliersQuery q, CancellationToken ct)
    {
        var query = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(s => s.Status.ToString() == q.Status);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierListItemDto(
                s.Id, s.SupplierRef, s.Name, s.SupplierType, s.Status.ToString(),
                s.IsOutsourcedClinicalService, s.BranchId, s.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetSupplierByIdQuery(Guid SupplierId) : IQuery<SupplierDetailDto>;

public sealed class GetSupplierByIdHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetSupplierByIdQuery, SupplierDetailDto>
{
    public async Task<SupplierDetailDto> Handle(GetSupplierByIdQuery q, CancellationToken ct)
    {
        var s = await db.Suppliers.AsNoTracking()
            .Include(x => x.Certificates).Include(x => x.Contracts).Include(x => x.Cars)
            .SingleOrDefaultAsync(x => x.Id == q.SupplierId, ct)
            ?? throw new DomainException("SUP-404", "Supplier not found.");

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        return new SupplierDetailDto(
            s.Id, s.SupplierRef, s.Name, s.SupplierType, s.Status.ToString(),
            s.RegisteredBy, s.ApprovedBy, s.SuspensionReason,
            s.Certificates.Select(x => new CertificateDto(x.Id, x.CertificateType, x.ExpiresAt, x.FileId)).ToList(),
            s.IsOutsourcedClinicalService, s.ServiceScope,
            s.Contracts.OrderByDescending(x => x.StartDate)
                .Select(x => new SupplierContractDto(
                    x.Id, x.ContractRef, x.Title, x.StartDate, x.EndDate, x.SlaSummary,
                    x.Status.ToString(), x.TerminationReason, x.IsExpired(today)))
                .ToList(),
            s.Cars.OrderByDescending(x => x.RaisedOn)
                .Select(x => new SupplierCarDto(
                    x.Id, x.Description, x.RaisedOn, x.DueDate, x.Status.ToString(), x.ResponseNote,
                    x.ResponseOn, x.Effective, x.ClosureNote, x.IsOverdue(today)))
                .ToList());
    }
}

/// <summary>
/// Outsourced clinical-services oversight (HQMS M16): the suppliers that deliver an outsourced
/// clinical service (reference lab, radiology, dialysis…), each with its active-contract and
/// open-CAR counts and latest evaluation score — the governance view of externalised services.
/// </summary>
public sealed record GetOutsourcedServicesQuery : IQuery<IReadOnlyList<OutsourcedServiceDto>>;

public sealed class GetOutsourcedServicesHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetOutsourcedServicesQuery, IReadOnlyList<OutsourcedServiceDto>>
{
    public async Task<IReadOnlyList<OutsourcedServiceDto>> Handle(GetOutsourcedServicesQuery q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var suppliers = await db.Suppliers.AsNoTracking()
            .Include(s => s.Contracts).Include(s => s.Cars)
            .Where(s => s.IsOutsourcedClinicalService)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        // Latest evaluation score per supplier (the score of record for oversight).
        var supplierIds = suppliers.Select(s => s.Id).ToList();
        var evaluations = await db.SupplierEvaluations.AsNoTracking()
            .Where(e => supplierIds.Contains(e.SupplierId))
            .ToListAsync(ct);
        var latestScore = evaluations
            .GroupBy(e => e.SupplierId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.PeriodEnd).First().WeightedTotal);

        return suppliers.Select(s => new OutsourcedServiceDto(
            s.Id, s.SupplierRef, s.Name, s.ServiceScope, s.Status.ToString(),
            s.Contracts.Count(c => c.Status == ContractStatus.Active && !c.IsExpired(today)),
            s.OpenCarCount,
            latestScore.TryGetValue(s.Id, out var score) ? score : null)).ToList();
    }
}

public sealed record GetEvaluationsQuery(Guid SupplierId) : IQuery<IReadOnlyList<SupplierEvaluationDto>>;

public sealed class GetEvaluationsHandler(IAppDbContext db)
    : IQueryHandler<GetEvaluationsQuery, IReadOnlyList<SupplierEvaluationDto>>
{
    public async Task<IReadOnlyList<SupplierEvaluationDto>> Handle(
        GetEvaluationsQuery q, CancellationToken ct) =>
        await db.SupplierEvaluations.AsNoTracking()
            .Where(e => e.SupplierId == q.SupplierId)
            .OrderByDescending(e => e.PeriodEnd)
            .Select(e => new SupplierEvaluationDto(
                e.Id, e.SupplierId, e.PeriodStart, e.PeriodEnd,
                e.WeightedTotal, e.EvaluatedBy, e.Criteria))
            .ToListAsync(ct);
}
