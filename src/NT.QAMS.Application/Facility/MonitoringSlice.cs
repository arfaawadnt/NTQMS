using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Facility;
using NT.QAMS.Domain.Facility;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Facility;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record RegisterMonitoringPointCommand(
    string Name, string? Location, string Parameter, string Unit,
    decimal? LowLimit, decimal? HighLimit,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class RegisterMonitoringPointValidator : AbstractValidator<RegisterMonitoringPointCommand>
{
    public RegisterMonitoringPointValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Parameter).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
    }
}

public sealed class RegisterMonitoringPointHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<RegisterMonitoringPointCommand, Guid>
{
    public async Task<Guid> Handle(RegisterMonitoringPointCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var pointRef = await refs.NextAsync(tenantId, "ENV", ct);
        var point = MonitoringPoint.Register(
            pointRef, c.Name, c.Location, c.Parameter, c.Unit, c.LowLimit, c.HighLimit);
        point.BranchId = c.BranchId;
        point.DepartmentId = c.DepartmentId;
        db.MonitoringPoints.Add(point);
        await db.SaveChangesAsync(ct);
        return point.Id;
    }
}

[RequireInternalActor]
public sealed record SetMonitoringLimitsCommand(Guid PointId, decimal? LowLimit, decimal? HighLimit) : ICommand;
[RequireInternalActor]
public sealed record RecordReadingCommand(Guid PointId, decimal Value, string? Remark) : ICommand<Guid>;
[RequireInternalActor]
public sealed record SuspendMonitoringPointCommand(Guid PointId) : ICommand;
[RequireInternalActor]
public sealed record ResumeMonitoringPointCommand(Guid PointId) : ICommand;
[RequireInternalActor]
public sealed record RetireMonitoringPointCommand(Guid PointId) : ICommand;

public sealed class RecordReadingValidator : AbstractValidator<RecordReadingCommand>
{
    public RecordReadingValidator()
    {
        RuleFor(x => x.Remark).MaximumLength(1000);
    }
}

public sealed class MonitoringWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<SetMonitoringLimitsCommand>,
    ICommandHandler<RecordReadingCommand, Guid>,
    ICommandHandler<SuspendMonitoringPointCommand>,
    ICommandHandler<ResumeMonitoringPointCommand>,
    ICommandHandler<RetireMonitoringPointCommand>
{
    public async Task Handle(SetMonitoringLimitsCommand c, CancellationToken ct)
    {
        var point = await LoadAsync(c.PointId, ct);
        point.SetLimits(c.LowLimit, c.HighLimit);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(RecordReadingCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var point = await LoadAsync(c.PointId, ct);
        var readingId = point.RecordReading(c.Value, clock.UtcNow, actor, c.Remark);
        await db.SaveChangesAsync(ct);
        return readingId;
    }

    public async Task Handle(SuspendMonitoringPointCommand c, CancellationToken ct)
    {
        var point = await LoadAsync(c.PointId, ct);
        point.Suspend();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ResumeMonitoringPointCommand c, CancellationToken ct)
    {
        var point = await LoadAsync(c.PointId, ct);
        point.Resume();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RetireMonitoringPointCommand c, CancellationToken ct)
    {
        var point = await LoadAsync(c.PointId, ct);
        point.Retire();
        await db.SaveChangesAsync(ct);
    }

    private async Task<MonitoringPoint> LoadAsync(Guid id, CancellationToken ct) =>
        await db.MonitoringPoints.Include(p => p.Readings)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("ENV-404", "Monitoring point not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetMonitoringPointsQuery(string? Status)
    : IQuery<IReadOnlyList<MonitoringPointListItemDto>>;

public sealed class GetMonitoringPointsHandler(IAppDbContext db)
    : IQueryHandler<GetMonitoringPointsQuery, IReadOnlyList<MonitoringPointListItemDto>>
{
    public async Task<IReadOnlyList<MonitoringPointListItemDto>> Handle(
        GetMonitoringPointsQuery q, CancellationToken ct)
    {
        var points = db.MonitoringPoints.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<MonitoringPointStatus>(q.Status, ignoreCase: true, out var status))
        {
            points = points.Where(p => p.Status == status);
        }

        return await points
            .OrderBy(p => p.PointRef)
            .Select(p => new MonitoringPointListItemDto(
                p.Id, p.PointRef, p.Name, p.Location, p.Parameter, p.Unit,
                p.LowLimit, p.HighLimit, p.Status.ToString(),
                p.Readings.OrderByDescending(r => r.RecordedAtUtc).Select(r => (decimal?)r.Value).FirstOrDefault(),
                p.Readings.OrderByDescending(r => r.RecordedAtUtc).Select(r => (DateTimeOffset?)r.RecordedAtUtc).FirstOrDefault(),
                p.Readings.OrderByDescending(r => r.RecordedAtUtc).Select(r => (bool?)r.InLimit).FirstOrDefault(),
                p.Readings.Count(r => !r.InLimit),
                p.BranchId, p.DepartmentId))
            .ToListAsync(ct);
    }
}

public sealed record GetMonitoringPointByIdQuery(Guid PointId) : IQuery<MonitoringPointDetailDto>;

public sealed class GetMonitoringPointByIdHandler(IAppDbContext db)
    : IQueryHandler<GetMonitoringPointByIdQuery, MonitoringPointDetailDto>
{
    /// <summary>Recent-history window returned to the workspace (readings stay complete in the store).</summary>
    public const int ReadingWindow = 100;

    public async Task<MonitoringPointDetailDto> Handle(GetMonitoringPointByIdQuery q, CancellationToken ct)
    {
        var p = await db.MonitoringPoints.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.PointId, ct)
            ?? throw new DomainException("ENV-404", "Monitoring point not found.");

        var readings = await db.MonitoringPoints.AsNoTracking()
            .Where(x => x.Id == q.PointId)
            .SelectMany(x => x.Readings)
            .OrderByDescending(r => r.RecordedAtUtc)
            .Take(ReadingWindow)
            .Select(r => new EnvironmentalReadingDto(
                r.Id, r.Value, r.RecordedAtUtc, r.RecordedById, r.InLimit, r.Remark))
            .ToListAsync(ct);

        return new MonitoringPointDetailDto(
            p.Id, p.PointRef, p.Name, p.Location, p.Parameter, p.Unit,
            p.LowLimit, p.HighLimit, p.Status.ToString(),
            p.BranchId, p.DepartmentId, readings);
    }
}
