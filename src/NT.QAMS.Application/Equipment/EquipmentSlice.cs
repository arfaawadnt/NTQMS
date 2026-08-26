using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Resources;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Equipment;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
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

[RequireInternalActor]
public sealed record LogCalibrationCommand(
    Guid EquipmentId, DateOnly PerformedAt, string Provider, string Result, Guid? CertificateFileId)
    : ICommand;

[RequireInternalActor]
public sealed record LogMaintenanceCommand(
    Guid EquipmentId, DateOnly PerformedAt, string WorkDescription, Guid? CertificateFileId = null)
    : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class LogMaintenanceValidator : AbstractValidator<LogMaintenanceCommand>
{
    public LogMaintenanceValidator()
    {
        RuleFor(x => x.WorkDescription).NotEmpty().MaximumLength(2000);
    }
}

[RequireInternalActor]
public sealed record RetireEquipmentCommand(Guid EquipmentId) : ICommand;

internal static class EquipmentLoader
{
    public static async Task<EquipmentItem> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.EquipmentItems
            .Include(e => e.Calibrations)
            .Include(e => e.Maintenance)
            .Include(e => e.IntermediateChecks)
            .Include(e => e.Downtime)
            .Include(e => e.SafetyNotices)
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
        // Same guard as calibration: a certificate id must reference a stored
        // file, or the log would carry a download link that leads nowhere.
        if (c.CertificateFileId is { } fileId && !await db.Files.AnyAsync(f => f.Id == fileId, ct))
        {
            throw new DomainException("FILE-404", "Certificate file not found.");
        }

        (await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct))
            .LogMaintenance(c.PerformedAt, c.WorkDescription, c.CertificateFileId);
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

[RequireInternalActor]
public sealed record RecordIntermediateCheckCommand(
    Guid EquipmentId, DateOnly PerformedOn, string CheckType, bool Passed,
    Guid? ReferenceStandardId, string? Remarks) : ICommand<Guid>;

public sealed class RecordIntermediateCheckValidator : AbstractValidator<RecordIntermediateCheckCommand>
{
    public RecordIntermediateCheckValidator()
    {
        RuleFor(x => x.CheckType).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Remarks).MaximumLength(2000);
    }
}

public sealed class RecordIntermediateCheckHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<RecordIntermediateCheckCommand, Guid>
{
    public async Task<Guid> Handle(RecordIntermediateCheckCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        if (c.ReferenceStandardId is { } standardId)
        {
            var standard = await db.ReferenceStandards
                .FirstOrDefaultAsync(s => s.Id == standardId, ct)
                ?? throw new DomainException("RS-404", "Reference standard not found.");
            if (standard.Status != ReferenceStandardStatus.Active)
            {
                throw new DomainException("RS-020",
                    $"Standard {standard.StandardRef} is {standard.Status} â€” checks must use an active, in-date standard.");
            }
        }

        var equipment = await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct);
        var checkId = equipment.RecordIntermediateCheck(
            c.PerformedOn, actor, c.CheckType, c.Passed, c.ReferenceStandardId, c.Remarks);
        await db.SaveChangesAsync(ct);
        return checkId;
    }
}

// â”€â”€ Downtime & availability (HQMS M14) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record StartDowntimeCommand(Guid EquipmentId, DateTimeOffset StartedAtUtc, DowntimeCategory Category, string Reason)
    : ICommand<Guid>;

public sealed class StartDowntimeValidator : AbstractValidator<StartDowntimeCommand>
{
    public StartDowntimeValidator() => RuleFor(x => x.Reason).MaximumLength(1000);
}

public sealed class StartDowntimeHandler(IAppDbContext db) : ICommandHandler<StartDowntimeCommand, Guid>
{
    public async Task<Guid> Handle(StartDowntimeCommand c, CancellationToken ct)
    {
        var equipment = await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct);
        var id = equipment.StartDowntime(c.StartedAtUtc, c.Category, c.Reason);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

[RequireInternalActor]
public sealed record EndDowntimeCommand(Guid EquipmentId, Guid DowntimeId, DateTimeOffset EndedAtUtc) : ICommand;

public sealed class EndDowntimeHandler(IAppDbContext db) : ICommandHandler<EndDowntimeCommand>
{
    public async Task Handle(EndDowntimeCommand c, CancellationToken ct)
    {
        (await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct)).EndDowntime(c.DowntimeId, c.EndedAtUtc);
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Recalls & field safety notices (HQMS M14) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record LogSafetyNoticeCommand(
    Guid EquipmentId, SafetyNoticeType Type, string Reference, string Issuer,
    SafetyNoticeSeverity Severity, DateOnly ReceivedOn, DateOnly? RequiredActionBy) : ICommand<Guid>;

public sealed class LogSafetyNoticeValidator : AbstractValidator<LogSafetyNoticeCommand>
{
    public LogSafetyNoticeValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Issuer).MaximumLength(200);
    }
}

public sealed class LogSafetyNoticeHandler(IAppDbContext db) : ICommandHandler<LogSafetyNoticeCommand, Guid>
{
    public async Task<Guid> Handle(LogSafetyNoticeCommand c, CancellationToken ct)
    {
        var equipment = await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct);
        var id = equipment.LogSafetyNotice(c.Type, c.Reference, c.Issuer, c.Severity, c.ReceivedOn, c.RequiredActionBy);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

[RequireInternalActor]
public sealed record ActionSafetyNoticeCommand(Guid EquipmentId, Guid NoticeId, string Note, DateOnly On) : ICommand;

public sealed class ActionSafetyNoticeValidator : AbstractValidator<ActionSafetyNoticeCommand>
{
    public ActionSafetyNoticeValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
}

public sealed class ActionSafetyNoticeHandler(IAppDbContext db) : ICommandHandler<ActionSafetyNoticeCommand>
{
    public async Task Handle(ActionSafetyNoticeCommand c, CancellationToken ct)
    {
        (await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct)).ActionSafetyNotice(c.NoticeId, c.Note, c.On);
        await db.SaveChangesAsync(ct);
    }
}

[RequireInternalActor]
public sealed record CloseSafetyNoticeCommand(Guid EquipmentId, Guid NoticeId) : ICommand;

public sealed class CloseSafetyNoticeHandler(IAppDbContext db) : ICommandHandler<CloseSafetyNoticeCommand>
{
    public async Task Handle(CloseSafetyNoticeCommand c, CancellationToken ct)
    {
        (await EquipmentLoader.LoadAsync(db, c.EquipmentId, ct)).CloseSafetyNotice(c.NoticeId);
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetEquipmentQuery(
    string? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<EquipmentListItemDto>>;

public sealed class GetEquipmentHandler(IAppDbContext db)
    : IQueryHandler<GetEquipmentQuery, Contracts.Common.PagedResponse<EquipmentListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<EquipmentListItemDto>> Handle(GetEquipmentQuery q, CancellationToken ct)
    {
        var query = db.EquipmentItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(e => e.Status.ToString() == q.Status);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderBy(e => e.Code)
            .Select(e => new EquipmentListItemDto(
                e.Id, e.Code, e.Name, e.SerialNumber, e.Location,
                e.Status.ToString(), e.NextCalibrationDue, e.BranchId, e.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetEquipmentByIdQuery(Guid EquipmentId) : IQuery<EquipmentDetailDto>;

public sealed class GetEquipmentByIdHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetEquipmentByIdQuery, EquipmentDetailDto>
{
    public async Task<EquipmentDetailDto> Handle(GetEquipmentByIdQuery q, CancellationToken ct)
    {
        var e = await db.EquipmentItems
            .AsNoTracking()
            .Include(x => x.Calibrations)
            .Include(x => x.Maintenance)
            .Include(x => x.IntermediateChecks)
            .Include(x => x.Downtime)
            .Include(x => x.SafetyNotices)
            .SingleOrDefaultAsync(x => x.Id == q.EquipmentId, ct)
            ?? throw new DomainException("EQP-404", "Equipment not found.");

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        // Availability over the trailing 30 days, as a whole-number percentage.
        var availability30d = decimal.Round((decimal)e.Availability(now.AddDays(-30), now) * 100, 1);

        return new EquipmentDetailDto(
            e.Id, e.Code, e.Name, e.SerialNumber, e.Location, e.Status.ToString(),
            e.CalibrationIntervalDays, e.GracePeriodDays, e.LastCalibrationAt, e.NextCalibrationDue,
            e.Calibrations.OrderByDescending(x => x.PerformedAt)
                .Select(x => new CalibrationRecordDto(x.Id, x.PerformedAt, x.Provider, x.Result, x.CertificateFileId))
                .ToList(),
            e.Maintenance.OrderByDescending(x => x.PerformedAt)
                .Select(x => new MaintenanceRecordDto(x.Id, x.PerformedAt, x.WorkDescription, x.CertificateFileId))
                .ToList(),
            e.IntermediateChecks.OrderByDescending(x => x.PerformedOn)
                .Select(x => new IntermediateCheckDto(
                    x.Id, x.PerformedOn, x.PerformedById, x.CheckType, x.Passed, x.ReferenceStandardId, x.Remarks))
                .ToList(),
            e.Downtime.OrderByDescending(x => x.StartedAtUtc)
                .Select(x => new DowntimeEventDto(
                    x.Id, x.StartedAtUtc, x.EndedAtUtc, x.Category.ToString(), x.Reason, x.IsOpen,
                    decimal.Round((decimal)x.DurationHours(now), 1)))
                .ToList(),
            e.SafetyNotices.OrderByDescending(x => x.ReceivedOn)
                .Select(x => new SafetyNoticeDto(
                    x.Id, x.Type.ToString(), x.Reference, x.Issuer, x.Severity.ToString(), x.ReceivedOn,
                    x.RequiredActionBy, x.Status.ToString(), x.ActionNote, x.ActionedOn, x.IsOverdue(today)))
                .ToList(),
            availability30d);
    }
}

/// <summary>
/// The recall / field-safety-notice register (HQMS M14): every open safety notice across the
/// equipment fleet, so overdue actions can be chased. Flattened from the owning equipment items.
/// </summary>
public sealed record GetOpenSafetyNoticesQuery : IQuery<IReadOnlyList<OpenSafetyNoticeDto>>;

public sealed class GetOpenSafetyNoticesHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetOpenSafetyNoticesQuery, IReadOnlyList<OpenSafetyNoticeDto>>
{
    public async Task<IReadOnlyList<OpenSafetyNoticeDto>> Handle(GetOpenSafetyNoticesQuery q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var items = await db.EquipmentItems.AsNoTracking().Include(e => e.SafetyNotices)
            .Where(e => e.SafetyNotices.Any(n => n.Status != SafetyNoticeStatus.Closed))
            .ToListAsync(ct);

        return items
            .SelectMany(e => e.SafetyNotices
                .Where(n => n.Status != SafetyNoticeStatus.Closed)
                .Select(n => new OpenSafetyNoticeDto(
                    e.Id, e.Code, e.Name, n.Id, n.Type.ToString(), n.Reference, n.Issuer, n.Severity.ToString(),
                    n.ReceivedOn, n.RequiredActionBy, n.Status.ToString(), n.IsOverdue(today))))
            .OrderByDescending(n => n.IsOverdue)
            .ThenBy(n => n.RequiredActionBy ?? DateOnly.MaxValue)
            .ToList();
    }
}
