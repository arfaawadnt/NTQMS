using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.PatientSafety;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Domain.PatientSafety;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.PatientSafety;

// ── Commands ─────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.PatientSafety, PermissionAction.Create)]
public sealed record ReportFallCommand(
    string PatientRef, string Unit, DateTimeOffset OccurredAtUtc, HarmLevel Harm, string Description, Guid? DepartmentId)
    : ICommand<Guid>;

public sealed class ReportFallValidator : AbstractValidator<ReportFallCommand>
{
    public ReportFallValidator()
    {
        RuleFor(x => x.PatientRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public sealed class ReportFallHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ReportFallCommand, Guid>
{
    public async Task<Guid> Handle(ReportFallCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var eventRef = await refs.NextAsync(tenantId, "PSE", ct);
        var e = PatientSafetyEvent.ReportFall(eventRef, c.PatientRef, c.Unit, c.OccurredAtUtc, c.Harm, c.Description, c.DepartmentId);
        db.PatientSafetyEvents.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.PatientSafety, PermissionAction.Create)]
public sealed record ReportPressureInjuryCommand(
    string PatientRef, string Unit, DateTimeOffset OccurredAtUtc, HarmLevel Harm, string Description,
    PressureInjuryStage Stage, InjuryOrigin Origin, Guid? DepartmentId) : ICommand<Guid>;

public sealed class ReportPressureInjuryValidator : AbstractValidator<ReportPressureInjuryCommand>
{
    public ReportPressureInjuryValidator()
    {
        RuleFor(x => x.PatientRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public sealed class ReportPressureInjuryHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ReportPressureInjuryCommand, Guid>
{
    public async Task<Guid> Handle(ReportPressureInjuryCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var eventRef = await refs.NextAsync(tenantId, "PSE", ct);
        var e = PatientSafetyEvent.ReportPressureInjury(
            eventRef, c.PatientRef, c.Unit, c.OccurredAtUtc, c.Harm, c.Description, c.Stage, c.Origin, c.DepartmentId);
        db.PatientSafetyEvents.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.PatientSafety, PermissionAction.Edit)]
public sealed record ReviewSafetyEventCommand(Guid EventId, string Notes) : ICommand;

public sealed class ReviewSafetyEventValidator : AbstractValidator<ReviewSafetyEventCommand>
{
    public ReviewSafetyEventValidator() => RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
}

public sealed class ReviewSafetyEventHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<ReviewSafetyEventCommand>
{
    public async Task Handle(ReviewSafetyEventCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var e = await Load(db, c.EventId, ct);
        e.RecordReview(actor, c.Notes, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<PatientSafetyEvent> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.PatientSafetyEvents.SingleOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new DomainException("PSE-404", "Safety event not found.");
}

[RequirePermissionPolicy(PermissionCatalog.PatientSafety, PermissionAction.Void)]
public sealed record CloseSafetyEventCommand(Guid EventId) : ICommand;

public sealed class CloseSafetyEventHandler(IAppDbContext db) : ICommandHandler<CloseSafetyEventCommand>
{
    public async Task Handle(CloseSafetyEventCommand c, CancellationToken ct)
    {
        (await ReviewSafetyEventHandler.Load(db, c.EventId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

// M-10: register-scale — a hospital tenant's lifetime of events pages.
public sealed record GetSafetyEventsQuery(
    string? Type = null, string? Status = null,
    int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<SafetyEventListItemDto>>;

public sealed class GetSafetyEventsHandler(IAppDbContext db)
    : IQueryHandler<GetSafetyEventsQuery, Contracts.Common.PagedResponse<SafetyEventListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<SafetyEventListItemDto>> Handle(
        GetSafetyEventsQuery q, CancellationToken ct)
    {
        var query = db.PatientSafetyEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            query = query.Where(e => e.Type.ToString() == q.Type);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(e => e.Status.ToString() == q.Status);
        }

        return await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => new SafetyEventListItemDto(
                e.Id, e.EventRef, e.Type.ToString(), e.PatientRef, e.Unit, e.OccurredAtUtc,
                e.HarmLevel.ToString(), e.Origin.ToString(),
                e.Stage != null ? e.Stage.ToString() : null, e.Status.ToString()))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetSafetyEventByIdQuery(Guid EventId) : IQuery<SafetyEventDetailDto>;

public sealed class GetSafetyEventByIdHandler(IAppDbContext db) : IQueryHandler<GetSafetyEventByIdQuery, SafetyEventDetailDto>
{
    public async Task<SafetyEventDetailDto> Handle(GetSafetyEventByIdQuery q, CancellationToken ct)
    {
        var e = await db.PatientSafetyEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.EventId, ct)
            ?? throw new DomainException("PSE-404", "Safety event not found.");

        return new SafetyEventDetailDto(
            e.Id, e.EventRef, e.Type.ToString(), e.PatientRef, e.Unit, e.DepartmentId, e.OccurredAtUtc,
            e.HarmLevel.ToString(), e.Origin.ToString(), e.Stage != null ? e.Stage.ToString() : null,
            e.Description, e.Status.ToString(), e.ReviewedBy, e.ReviewNotes, e.ReviewedAtUtc);
    }
}

/// <summary>
/// Rates per 1,000 patient-days over a window (HQMS M08). The denominator is the ADT-derived
/// patient-days from the M24 projection — the reason the integration hub is built first.
/// </summary>
public sealed record GetSafetyRatesQuery(int WindowDays = 30) : IQuery<SafetyRatesDto>;

public sealed class GetSafetyRatesHandler(IAppDbContext db, IClock clock) : IQueryHandler<GetSafetyRatesQuery, SafetyRatesDto>
{
    public async Task<SafetyRatesDto> Handle(GetSafetyRatesQuery q, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var from = now.AddDays(-Math.Clamp(q.WindowDays, 1, 366));

        // Denominator: patient-days from the ADT projection, clipped to the window.
        var stays = await db.PatientStays.AsNoTracking()
            .Where(s => s.DischargedAtUtc == null || s.DischargedAtUtc >= from)
            .ToListAsync(ct);
        var patientDays = stays.Sum(s => s.PatientDaysInWindow(from, now));

        var events = await db.PatientSafetyEvents.AsNoTracking()
            .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc <= now)
            .Select(e => new { e.Type, e.Origin })
            .ToListAsync(ct);

        var falls = events.Count(e => e.Type == SafetyEventType.Fall);
        var pi = events.Count(e => e.Type == SafetyEventType.PressureInjury);
        var hapi = events.Count(e => e.Type == SafetyEventType.PressureInjury && e.Origin == InjuryOrigin.HospitalAcquired);

        return new SafetyRatesDto(
            from, now, patientDays,
            new SafetyRateDto("Fall", falls, patientDays, Rate(falls, patientDays)),
            new SafetyRateDto("PressureInjury", pi, patientDays, Rate(pi, patientDays)),
            hapi, Rate(hapi, patientDays));
    }

    // M-18: no denominator means no rate — a fabricated 0.00 is
    // indistinguishable from a genuinely zero event rate.
    private static decimal? Rate(int count, int patientDays) =>
        patientDays == 0 ? null : decimal.Round(count * 1000m / patientDays, 2);
}
