using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Integration;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Integration;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Integration;

// ── Endpoint management ──────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Integration, PermissionAction.Create)]
public sealed record RegisterEndpointCommand(string Name, InterfaceSystem System, InterfaceProtocol Protocol)
    : ICommand<Guid>;

public sealed class RegisterEndpointValidator : AbstractValidator<RegisterEndpointCommand>
{
    public RegisterEndpointValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}

public sealed class RegisterEndpointHandler(IAppDbContext db) : ICommandHandler<RegisterEndpointCommand, Guid>
{
    public async Task<Guid> Handle(RegisterEndpointCommand c, CancellationToken ct)
    {
        var endpoint = IntegrationEndpoint.Register(c.Name, c.System, c.Protocol);
        db.IntegrationEndpoints.Add(endpoint);
        await db.SaveChangesAsync(ct);
        return endpoint.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Integration, PermissionAction.Edit)]
public sealed record SuspendEndpointCommand(Guid EndpointId) : ICommand;

public sealed class SuspendEndpointHandler(IAppDbContext db) : ICommandHandler<SuspendEndpointCommand>
{
    public async Task Handle(SuspendEndpointCommand c, CancellationToken ct)
    {
        (await Load(db, c.EndpointId, ct)).Suspend();
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<IntegrationEndpoint> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.IntegrationEndpoints.SingleOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new DomainException("INT-404", "Integration endpoint not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Integration, PermissionAction.Edit)]
public sealed record ResumeEndpointCommand(Guid EndpointId) : ICommand;

public sealed class ResumeEndpointHandler(IAppDbContext db) : ICommandHandler<ResumeEndpointCommand>
{
    public async Task Handle(ResumeEndpointCommand c, CancellationToken ct)
    {
        (await SuspendEndpointHandler.Load(db, c.EndpointId, ct)).Resume();
        await db.SaveChangesAsync(ct);
    }
}

// ── ADT ingestion (called by the protocol adapter) ───────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Integration, PermissionAction.Create)]
public sealed record IngestAdtEventCommand(
    Guid EndpointId, string DedupKey, string MessageType, string RawPayload,
    AdtEventType EventType, string PatientRef, string EncounterRef, string Unit,
    Guid? DepartmentId, DateTimeOffset EventAtUtc) : ICommand<IngestResultDto>;

public sealed class IngestAdtEventValidator : AbstractValidator<IngestAdtEventCommand>
{
    public IngestAdtEventValidator(IClock clock)
    {
        RuleFor(x => x.DedupKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MessageType).MaximumLength(40);
        RuleFor(x => x.PatientRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EncounterRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(100);
        // A future event time poisons every windowed rate denominator (M-03); the
        // 5-minute tolerance mirrors INC-005's clock-skew allowance.
        RuleFor(x => x.EventAtUtc)
            .Must(t => t <= clock.UtcNow.AddMinutes(5))
            .WithMessage("The event time cannot be in the future.");
    }
}

/// <summary>
/// Records an inbound ADT event and applies it to the patient-stay projection. Idempotent by
/// (endpoint, dedup key): a message already processed is a no-op; a previously failed one is
/// reprocessed (the retry path). Processing errors are captured on the message (Failed) and
/// on the endpoint health rather than thrown, so a bad message never loses the record.
/// </summary>
public sealed class IngestAdtEventHandler(IAppDbContext db, IClock clock)
    : ICommandHandler<IngestAdtEventCommand, IngestResultDto>
{
    public async Task<IngestResultDto> Handle(IngestAdtEventCommand c, CancellationToken ct)
    {
        var endpoint = await SuspendEndpointHandler.Load(db, c.EndpointId, ct);
        if (endpoint.Status != EndpointStatus.Active)
        {
            throw new DomainException("INT-005", "The endpoint is suspended; resume it before ingesting.");
        }

        var dedup = c.DedupKey.Trim();
        var message = await db.IntegrationMessages
            .FirstOrDefaultAsync(m => m.EndpointId == c.EndpointId && m.DedupKey == dedup, ct);

        if (message is { Status: MessageStatus.Processed })
        {
            return new IngestResultDto(message.Id, message.Status.ToString(), null);
        }

        if (message is null)
        {
            message = IntegrationMessage.Receive(c.EndpointId, dedup, c.MessageType, c.RawPayload, clock.UtcNow);
            db.IntegrationMessages.Add(message);
        }

        try
        {
            await ApplyAsync(c, ct);
            message.MarkProcessed(clock.UtcNow);
            endpoint.RecordSuccess(clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return new IngestResultDto(message.Id, message.Status.ToString(), null);
        }
        catch (DomainException ex)
        {
            message.MarkFailed(ex.Message, clock.UtcNow);
            endpoint.RecordFailure(clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return new IngestResultDto(message.Id, message.Status.ToString(), ex.Message);
        }
    }

    private async Task ApplyAsync(IngestAdtEventCommand c, CancellationToken ct)
    {
        var encounter = c.EncounterRef.Trim();
        var stay = await db.PatientStays.FirstOrDefaultAsync(s => s.EncounterRef == encounter, ct);

        switch (c.EventType)
        {
            case AdtEventType.Admit:
                if (stay is null)
                {
                    db.PatientStays.Add(PatientStay.Admit(c.PatientRef, encounter, c.Unit, c.DepartmentId, c.EventAtUtc));
                }
                else
                {
                    stay.Transfer(c.Unit, c.DepartmentId); // A repeated/updated admit refreshes the unit.
                }

                break;

            case AdtEventType.Transfer:
                (stay ?? throw new DomainException("STAY-020", "No stay found for the transferred encounter."))
                    .Transfer(c.Unit, c.DepartmentId);
                break;

            case AdtEventType.Discharge:
                (stay ?? throw new DomainException("STAY-021", "No stay found for the discharged encounter."))
                    .Discharge(c.EventAtUtc);
                break;

            default:
                throw new DomainException("STAY-022", "Unsupported ADT event type.");
        }
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetEndpointsQuery : IQuery<IReadOnlyList<EndpointListItemDto>>;

public sealed class GetEndpointsHandler(IAppDbContext db) : IQueryHandler<GetEndpointsQuery, IReadOnlyList<EndpointListItemDto>>
{
    public async Task<IReadOnlyList<EndpointListItemDto>> Handle(GetEndpointsQuery q, CancellationToken ct)
    {
        var endpoints = await db.IntegrationEndpoints.AsNoTracking().OrderBy(e => e.Name).ToListAsync(ct);

        var counts = (await db.IntegrationMessages.AsNoTracking()
                .Select(m => new { m.EndpointId, m.Status })
                .ToListAsync(ct))
            .GroupBy(m => m.EndpointId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Received: g.Count(x => x.Status == MessageStatus.Received),
                    Processed: g.Count(x => x.Status == MessageStatus.Processed),
                    Failed: g.Count(x => x.Status == MessageStatus.Failed)));

        return endpoints
            .Select(e =>
            {
                counts.TryGetValue(e.Id, out var c);
                return new EndpointListItemDto(
                    e.Id, e.Name, e.System.ToString(), e.Protocol.ToString(), e.Status.ToString(), e.IsHealthy,
                    e.LastMessageAtUtc, e.LastErrorAtUtc, e.ConsecutiveFailures, c.Received, c.Processed, c.Failed);
            })
            .ToList();
    }
}

public sealed record GetIntegrationMessagesQuery(Guid EndpointId, string? Status = null, int Take = 100)
    : IQuery<IReadOnlyList<IntegrationMessageDto>>;

public sealed class GetIntegrationMessagesHandler(IAppDbContext db)
    : IQueryHandler<GetIntegrationMessagesQuery, IReadOnlyList<IntegrationMessageDto>>
{
    public async Task<IReadOnlyList<IntegrationMessageDto>> Handle(GetIntegrationMessagesQuery q, CancellationToken ct)
    {
        var query = db.IntegrationMessages.AsNoTracking().Where(m => m.EndpointId == q.EndpointId);
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(m => m.Status.ToString() == q.Status);
        }

        return await query
            .OrderByDescending(m => m.ReceivedAtUtc)
            .Take(Math.Clamp(q.Take, 1, 500))
            .Select(m => new IntegrationMessageDto(
                m.Id, m.EndpointId, m.DedupKey, m.MessageType, m.Status.ToString(),
                m.ErrorDetail, m.ReceivedAtUtc, m.ProcessedAtUtc))
            .ToListAsync(ct);
    }
}

/// <summary>Reconciliation across endpoints: received vs processed vs failed message counts.</summary>
public sealed record GetReconciliationQuery : IQuery<IReadOnlyList<ReconciliationDto>>;

public sealed class GetReconciliationHandler(IAppDbContext db) : IQueryHandler<GetReconciliationQuery, IReadOnlyList<ReconciliationDto>>
{
    public async Task<IReadOnlyList<ReconciliationDto>> Handle(GetReconciliationQuery q, CancellationToken ct)
    {
        var endpoints = await db.IntegrationEndpoints.AsNoTracking()
            .Select(e => new { e.Id, e.Name }).ToListAsync(ct);
        var messages = await db.IntegrationMessages.AsNoTracking()
            .Select(m => new { m.EndpointId, m.Status }).ToListAsync(ct);

        return endpoints
            .Select(e =>
            {
                var mine = messages.Where(m => m.EndpointId == e.Id).ToList();
                return new ReconciliationDto(
                    e.Id, e.Name,
                    mine.Count(m => m.Status == MessageStatus.Received),
                    mine.Count(m => m.Status == MessageStatus.Processed),
                    mine.Count(m => m.Status == MessageStatus.Failed));
            })
            .ToList();
    }
}

/// <summary>Live census from the ADT projection: active stays now, and patient-days over a window.</summary>
public sealed record GetPatientCensusQuery(int WindowDays = 30) : IQuery<PatientCensusDto>;

public sealed class GetPatientCensusHandler(IAppDbContext db, IClock clock) : IQueryHandler<GetPatientCensusQuery, PatientCensusDto>
{
    public async Task<PatientCensusDto> Handle(GetPatientCensusQuery q, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var from = now.AddDays(-Math.Clamp(q.WindowDays, 1, 366));

        var stays = await db.PatientStays.AsNoTracking()
            .Where(s => s.DischargedAtUtc == null || s.DischargedAtUtc >= from)
            .ToListAsync(ct);

        var active = stays.Count(s => s.Status == StayStatus.Admitted);
        // Patient-days within the window — the canonical clamped accrual (M-03).
        var patientDays = stays.Sum(s => s.PatientDaysInWindow(from, now));

        return new PatientCensusDto(active, patientDays, now, from);
    }
}
