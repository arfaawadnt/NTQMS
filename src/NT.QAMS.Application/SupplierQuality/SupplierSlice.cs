using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.RiskGovernance;
using NT.QAMS.Contracts.Governance;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.SupplierQuality;

[RequireInternalActor]
public sealed record RegisterSupplierCommand(string Name, string SupplierType,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class RegisterSupplierValidator : AbstractValidator<RegisterSupplierCommand>
{
    public RegisterSupplierValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public sealed class RegisterSupplierHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs)
    : ICommandHandler<RegisterSupplierCommand, Guid>
{
    public async Task<Guid> Handle(RegisterSupplierCommand c, CancellationToken ct)
    {
        var supplierRef = await refs.NextAsync(GovernanceHelpers.RequireTenant(tenant), "SUP", ct);
        var supplier = Supplier.Register(
            supplierRef, c.Name, c.SupplierType, GovernanceHelpers.RequireActor(user));
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
[RequireInternalActor]
public sealed record ApproveSupplierCommand(Guid SupplierId) : ICommand;
[RequireInternalActor]
public sealed record SuspendSupplierCommand(Guid SupplierId, string Reason) : ICommand;
[RequireInternalActor]
public sealed record RecordEvaluationCommand(
    Guid SupplierId, DateOnly PeriodStart, DateOnly PeriodEnd,
    IReadOnlyList<(string Criterion, decimal Weight, decimal Score)> Criteria) : ICommand<Guid>;

internal static class SupplierLoader
{
    public static async Task<Supplier> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Suppliers.Include(s => s.Certificates).SingleOrDefaultAsync(s => s.Id == id, ct)
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

public sealed class ApproveSupplierHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<ApproveSupplierCommand>
{
    public async Task Handle(ApproveSupplierCommand c, CancellationToken ct)
    {
        (await SupplierLoader.LoadAsync(db, c.SupplierId, ct))
            .Approve(GovernanceHelpers.RequireActor(user));
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

public sealed record GetSuppliersQuery(string? Status = null) : IQuery<IReadOnlyList<SupplierListItemDto>>;

public sealed class GetSuppliersHandler(IAppDbContext db)
    : IQueryHandler<GetSuppliersQuery, IReadOnlyList<SupplierListItemDto>>
{
    public async Task<IReadOnlyList<SupplierListItemDto>> Handle(GetSuppliersQuery q, CancellationToken ct)
    {
        var query = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(s => s.Status.ToString() == q.Status);
        }

        return await query
            .OrderBy(s => s.Name)
            .Take(500)
            .Select(s => new SupplierListItemDto(
                s.Id, s.SupplierRef, s.Name, s.SupplierType, s.Status.ToString(), s.BranchId, s.DepartmentId))
            .ToListAsync(ct);
    }
}

public sealed record GetSupplierByIdQuery(Guid SupplierId) : IQuery<SupplierDetailDto>;

public sealed class GetSupplierByIdHandler(IAppDbContext db)
    : IQueryHandler<GetSupplierByIdQuery, SupplierDetailDto>
{
    public async Task<SupplierDetailDto> Handle(GetSupplierByIdQuery q, CancellationToken ct)
    {
        var s = await db.Suppliers.AsNoTracking().Include(x => x.Certificates)
            .SingleOrDefaultAsync(x => x.Id == q.SupplierId, ct)
            ?? throw new DomainException("SUP-404", "Supplier not found.");

        return new SupplierDetailDto(
            s.Id, s.SupplierRef, s.Name, s.SupplierType, s.Status.ToString(),
            s.RegisteredBy, s.ApprovedBy, s.SuspensionReason,
            s.Certificates.Select(x => new CertificateDto(x.Id, x.CertificateType, x.ExpiresAt, x.FileId))
                .ToList());
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
                e.WeightedTotal, e.EvaluatedBy, e.CriteriaJson))
            .ToListAsync(ct);
}
