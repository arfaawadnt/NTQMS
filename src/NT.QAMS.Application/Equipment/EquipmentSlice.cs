using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Resources;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Equipment;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record RegisterEquipmentCommand(
    string Name, string SerialNumber, string? Location,
    int CalibrationIntervalDays, int GracePeriodDays,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class RegisterEquipmentValidator : AbstractValidator<RegisterEquipmentCommand>
{
    public RegisterEquipmentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CalibrationIntervalDays).InclusiveBetween(1, 3650);
        RuleFor(x => x.GracePeriodDays).InclusiveBetween(0, 365);
    }
}

public sealed class RegisterEquipmentHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<RegisterEquipmentCommand, Guid>
{
    public async Task<Guid> Handle(RegisterEquipmentCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        var serial = c.SerialNumber.Trim();
        if (await db.EquipmentItems.AnyAsync(e => e.SerialNumber == serial, ct))
        {
            throw new DomainException("EQP-004", $"Serial number '{serial}' is already registered.");
        }

        var code = await refs.NextAsync(tenantId, "EQP", ct);
        var item = EquipmentItem.Register(
            code, c.Name, serial, c.Location, c.CalibrationIntervalDays, c.GracePeriodDays);

        item.BranchId = c.BranchId;
        item.DepartmentId = c.DepartmentId;
        db.EquipmentItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }
}

public sealed record LogCalibrationCommand(
    Guid EquipmentId, DateOnly PerformedAt, string Provider, string Result, Guid? CertificateFileId)
    : ICommand;

public sealed record LogMaintenanceCommand(Guid EquipmentId, DateOnly PerformedAt, string WorkDescription)
    : ICommand;

public sealed record RetireEquipmentCommand(Guid EquipmentId) : ICommand;

internal static class EquipmentLoader
{
    public static async Task<EquipmentItem> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.EquipmentItems
            .Include(e => e.Calibrations)
            .Include(e => e.Maintenance)
            .SingleOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new DomainException("EQP-404", "Equipment not found.");
}

public sealed class LogCalibrationHandler(IAppDbContext db) : ICommandHandler<LogCalibrationCommand>
{
    public async Task Handle(LogCalibrationCommand c, CancellationToken ct)
    {
        if (c.CertificateFileId is { } fileId && !await db.Files.AnyAsync(f => f.Id == fileId, ct))
        {
            throw new DomainException("FILE-404", "Certificate file not found.");
        }

        (await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct))
            .LogCalibration(c.PerformedAt, c.Provider, c.Result, c.CertificateFileId);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class LogMaintenanceHandler(IAppDbContext db) : ICommandHandler<LogMaintenanceCommand>
{
    public async Task Handle(LogMaintenanceCommand c, CancellationToken ct)
    {
        (await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct))
            .LogMaintenance(c.PerformedAt, c.WorkDescription);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RetireEquipmentHandler(IAppDbContext db) : ICommandHandler<RetireEquipmentCommand>
{
    public async Task Handle(RetireEquipmentCommand c, CancellationToken ct)
    {
        (await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct)).Retire();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetEquipmentQuery(string? Status = null) : IQuery<IReadOnlyList<EquipmentListItemDto>>;

public sealed class GetEquipmentHandler(IAppDbContext db)
    : IQueryHandler<GetEquipmentQuery, IReadOnlyList<EquipmentListItemDto>>
{
    public async Task<IReadOnlyList<EquipmentListItemDto>> Handle(GetEquipmentQuery q, CancellationToken ct)
    {
        var query = db.EquipmentItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(e => e.Status.ToString() == q.Status);
        }

        return await query
            .OrderBy(e => e.Code)
            .Take(500)
            .Select(e => new EquipmentListItemDto(
                e.Id, e.Code, e.Name, e.SerialNumber, e.Location,
                e.Status.ToString(), e.NextCalibrationDue, e.BranchId, e.DepartmentId))
            .ToListAsync(ct);
    }
}

public sealed record GetEquipmentByIdQuery(Guid EquipmentId) : IQuery<EquipmentDetailDto>;

public sealed class GetEquipmentByIdHandler(IAppDbContext db)
    : IQueryHandler<GetEquipmentByIdQuery, EquipmentDetailDto>
{
    public async Task<EquipmentDetailDto> Handle(GetEquipmentByIdQuery q, CancellationToken ct)
    {
        var e = await db.EquipmentItems
            .AsNoTracking()
            .Include(x => x.Calibrations)
            .Include(x => x.Maintenance)
            .SingleOrDefaultAsync(x => x.Id == q.EquipmentId, ct)
            ?? throw new DomainException("EQP-404", "Equipment not found.");

        return new EquipmentDetailDto(
            e.Id, e.Code, e.Name, e.SerialNumber, e.Location, e.Status.ToString(),
            e.CalibrationIntervalDays, e.GracePeriodDays, e.LastCalibrationAt, e.NextCalibrationDue,
            e.Calibrations.OrderByDescending(x => x.PerformedAt)
                .Select(x => new CalibrationRecordDto(x.Id, x.PerformedAt, x.Provider, x.Result, x.CertificateFileId))
                .ToList(),
            e.Maintenance.OrderByDescending(x => x.PerformedAt)
                .Select(x => new MaintenanceRecordDto(x.Id, x.PerformedAt, x.WorkDescription))
                .ToList());
    }
}
