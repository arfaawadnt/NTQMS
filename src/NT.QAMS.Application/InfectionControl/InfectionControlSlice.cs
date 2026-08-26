using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.InfectionControl;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.InfectionControl;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.InfectionControl;

// ── HAI case commands ─────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.InfectionControl, PermissionAction.Create)]
public sealed record ReportHaiCaseCommand(
    HaiType Type, string PatientRef, string Unit, DateTimeOffset OnsetDateUtc,
    string? Organism, string Description, Guid? DepartmentId) : ICommand<Guid>;

public sealed class ReportHaiCaseValidator : AbstractValidator<ReportHaiCaseCommand>
{
    public ReportHaiCaseValidator()
    {
        RuleFor(x => x.PatientRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(100);
        RuleFor(x => x.Organism).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public sealed class ReportHaiCaseHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ReportHaiCaseCommand, Guid>
{
    public async Task<Guid> Handle(ReportHaiCaseCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var caseRef = await refs.NextAsync(tenantId, "HAI", ct);
        var hai = HaiCase.Report(caseRef, c.Type, c.PatientRef, c.Unit, c.OnsetDateUtc, c.Organism, c.Description, c.DepartmentId);
        db.HaiCases.Add(hai);
        await db.SaveChangesAsync(ct);
        return hai.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.InfectionControl, PermissionAction.Edit)]
public sealed record ReviewHaiCaseCommand(Guid CaseId, string Notes) : ICommand;

public sealed class ReviewHaiCaseValidator : AbstractValidator<ReviewHaiCaseCommand>
{
    public ReviewHaiCaseValidator() => RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
}

public sealed class ReviewHaiCaseHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<ReviewHaiCaseCommand>
{
    public async Task Handle(ReviewHaiCaseCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var hai = await Load(db, c.CaseId, ct);
        hai.RecordReview(actor, c.Notes, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<HaiCase> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.HaiCases.SingleOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new DomainException("HAI-404", "HAI case not found.");
}

[RequirePermissionPolicy(PermissionCatalog.InfectionControl, PermissionAction.Void)]
public sealed record CloseHaiCaseCommand(Guid CaseId) : ICommand;

public sealed class CloseHaiCaseHandler(IAppDbContext db) : ICommandHandler<CloseHaiCaseCommand>
{
    public async Task Handle(CloseHaiCaseCommand c, CancellationToken ct)
    {
        (await ReviewHaiCaseHandler.Load(db, c.CaseId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

// ── Device-exposure commands (the device-day denominator) ─────────────────────

[RequirePermissionPolicy(PermissionCatalog.InfectionControl, PermissionAction.Create)]
public sealed record RecordDeviceExposureCommand(
    string PatientRef, string Unit, DeviceType DeviceType, DateTimeOffset InsertedAtUtc, Guid? DepartmentId)
    : ICommand<Guid>;

public sealed class RecordDeviceExposureValidator : AbstractValidator<RecordDeviceExposureCommand>
{
    public RecordDeviceExposureValidator()
    {
        RuleFor(x => x.PatientRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(100);
    }
}

public sealed class RecordDeviceExposureHandler(IAppDbContext db) : ICommandHandler<RecordDeviceExposureCommand, Guid>
{
    public async Task<Guid> Handle(RecordDeviceExposureCommand c, CancellationToken ct)
    {
        var exposure = DeviceExposure.Record(c.PatientRef, c.Unit, c.DeviceType, c.InsertedAtUtc, c.DepartmentId);
        db.DeviceExposures.Add(exposure);
        await db.SaveChangesAsync(ct);
        return exposure.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.InfectionControl, PermissionAction.Edit)]
public sealed record RemoveDeviceCommand(Guid ExposureId, DateTimeOffset RemovedAtUtc) : ICommand;

public sealed class RemoveDeviceHandler(IAppDbContext db) : ICommandHandler<RemoveDeviceCommand>
{
    public async Task Handle(RemoveDeviceCommand c, CancellationToken ct)
    {
        var exposure = await db.DeviceExposures.SingleOrDefaultAsync(e => e.Id == c.ExposureId, ct)
            ?? throw new DomainException("DEV-404", "Device exposure not found.");
        exposure.Remove(c.RemovedAtUtc);
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetHaiCasesQuery(string? Type = null, string? Status = null)
    : IQuery<IReadOnlyList<HaiCaseListItemDto>>;

public sealed class GetHaiCasesHandler(IAppDbContext db)
    : IQueryHandler<GetHaiCasesQuery, IReadOnlyList<HaiCaseListItemDto>>
{
    public async Task<IReadOnlyList<HaiCaseListItemDto>> Handle(GetHaiCasesQuery q, CancellationToken ct)
    {
        var query = db.HaiCases.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            query = query.Where(e => e.Type.ToString() == q.Type);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(e => e.Status.ToString() == q.Status);
        }

        return await query
            .OrderByDescending(e => e.OnsetDateUtc)
            .Select(e => new HaiCaseListItemDto(
                e.Id, e.CaseRef, e.Type.ToString(), e.PatientRef, e.Unit, e.OnsetDateUtc, e.Organism, e.Status.ToString()))
            .ToListAsync(ct);
    }
}

public sealed record GetHaiCaseByIdQuery(Guid CaseId) : IQuery<HaiCaseDetailDto>;

public sealed class GetHaiCaseByIdHandler(IAppDbContext db) : IQueryHandler<GetHaiCaseByIdQuery, HaiCaseDetailDto>
{
    public async Task<HaiCaseDetailDto> Handle(GetHaiCaseByIdQuery q, CancellationToken ct)
    {
        var e = await db.HaiCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.CaseId, ct)
            ?? throw new DomainException("HAI-404", "HAI case not found.");

        return new HaiCaseDetailDto(
            e.Id, e.CaseRef, e.Type.ToString(), e.PatientRef, e.Unit, e.DepartmentId, e.OnsetDateUtc,
            e.Organism, e.Description, e.Status.ToString(), e.ReviewedBy, e.ReviewNotes, e.ReviewedAtUtc);
    }
}

public sealed record GetDeviceExposuresQuery(string? DeviceType = null, string? Status = null)
    : IQuery<IReadOnlyList<DeviceExposureListItemDto>>;

public sealed class GetDeviceExposuresHandler(IAppDbContext db)
    : IQueryHandler<GetDeviceExposuresQuery, IReadOnlyList<DeviceExposureListItemDto>>
{
    public async Task<IReadOnlyList<DeviceExposureListItemDto>> Handle(GetDeviceExposuresQuery q, CancellationToken ct)
    {
        var query = db.DeviceExposures.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.DeviceType))
        {
            query = query.Where(e => e.DeviceType.ToString() == q.DeviceType);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(e => e.Status.ToString() == q.Status);
        }

        return await query
            .OrderByDescending(e => e.InsertedAtUtc)
            .Select(e => new DeviceExposureListItemDto(
                e.Id, e.PatientRef, e.Unit, e.DeviceType.ToString(), e.InsertedAtUtc, e.RemovedAtUtc, e.Status.ToString()))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Device-associated infection rates per 1,000 device-days over a window (HQMS M09). Device-days
/// come from the M09 device-exposure register; patient-days come from the M24 ADT projection —
/// so the module reports both the infection rate and the device-utilisation ratio that frames it.
/// </summary>
public sealed record GetHaiRatesQuery(int WindowDays = 30) : IQuery<HaiRatesDto>;

public sealed class GetHaiRatesHandler(IAppDbContext db, IClock clock) : IQueryHandler<GetHaiRatesQuery, HaiRatesDto>
{
    public async Task<HaiRatesDto> Handle(GetHaiRatesQuery q, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var from = now.AddDays(-Math.Clamp(q.WindowDays, 1, 366));

        // Patient-days denominator: the M24 ADT projection, clipped to the window.
        var stays = await db.PatientStays.AsNoTracking()
            .Where(s => s.DischargedAtUtc == null || s.DischargedAtUtc >= from)
            .Select(s => new { s.AdmittedAtUtc, s.DischargedAtUtc })
            .ToListAsync(ct);
        var patientDays = stays.Sum(s => ClippedDays(s.AdmittedAtUtc, s.DischargedAtUtc, from, now));

        // Device-days denominator: this module's device-exposure register, clipped to the window.
        var exposures = await db.DeviceExposures.AsNoTracking()
            .Where(e => e.RemovedAtUtc == null || e.RemovedAtUtc >= from)
            .Select(e => new { e.DeviceType, e.InsertedAtUtc, e.RemovedAtUtc })
            .ToListAsync(ct);

        int DeviceDays(DeviceType t) =>
            exposures.Where(e => e.DeviceType == t).Sum(e => ClippedDays(e.InsertedAtUtc, e.RemovedAtUtc, from, now));

        var cases = await db.HaiCases.AsNoTracking()
            .Where(e => e.OnsetDateUtc >= from && e.OnsetDateUtc <= now)
            .Select(e => e.Type)
            .ToListAsync(ct);

        HaiDeviceRateDto Build(HaiType hai, DeviceType device)
        {
            var deviceDays = DeviceDays(device);
            var count = cases.Count(t => t == hai);
            return new HaiDeviceRateDto(
                hai.ToString(), device.ToString(), deviceDays, count,
                Rate(count, deviceDays), Utilization(deviceDays, patientDays));
        }

        return new HaiRatesDto(
            from, now, patientDays,
            Build(HaiType.Clabsi, DeviceType.CentralLine),
            Build(HaiType.Cauti, DeviceType.UrinaryCatheter),
            Build(HaiType.Vap, DeviceType.Ventilator),
            cases.Count(t => t == HaiType.Ssi));
    }

    /// <summary>Whole days a span overlaps [from, now], counting a same-day span as one day.</summary>
    private static int ClippedDays(DateTimeOffset start, DateTimeOffset? end, DateTimeOffset from, DateTimeOffset now)
    {
        var clippedStart = start > from ? start : from;
        var clippedEnd = end ?? now;
        if (clippedEnd > now) { clippedEnd = now; }
        if (clippedEnd <= clippedStart) { return 0; }
        var days = (int)Math.Floor((clippedEnd - clippedStart).TotalDays);
        return days < 1 ? 1 : days;
    }

    private static decimal Rate(int count, int deviceDays) =>
        deviceDays == 0 ? 0m : decimal.Round(count * 1000m / deviceDays, 2);

    private static decimal Utilization(int deviceDays, int patientDays) =>
        patientDays == 0 ? 0m : decimal.Round((decimal)deviceDays / patientDays, 2);
}
