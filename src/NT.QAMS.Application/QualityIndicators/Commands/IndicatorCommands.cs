using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.QualityIndicators;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.QualityIndicators.Commands;

// ── Define ──────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Indicators, PermissionAction.Create)]
public sealed record DefineIndicatorCommand(
    string Code, string Name, string? Description,
    string Numerator, string Denominator, string Unit, decimal RateFactor,
    IndicatorFrequency Frequency, IndicatorDirection Direction,
    string? Inclusions = null, string? Exclusions = null, string? DataSource = null)
    : ICommand<Guid>;

public sealed class DefineIndicatorValidator : AbstractValidator<DefineIndicatorCommand>
{
    public DefineIndicatorValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Numerator).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Denominator).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RateFactor).GreaterThan(0m);
        RuleFor(x => x.Inclusions).MaximumLength(2000);
        RuleFor(x => x.Exclusions).MaximumLength(2000);
        RuleFor(x => x.DataSource).MaximumLength(1000);
    }
}

public sealed class DefineIndicatorHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<DefineIndicatorCommand, Guid>
{
    public async Task<Guid> Handle(DefineIndicatorCommand command, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        var code = command.Code.Trim();
        if (await db.QualityIndicators.AnyAsync(i => i.Code == code, ct))
        {
            throw new DomainException("IND-020", $"An indicator with code '{code}' already exists.");
        }

        var indicatorRef = await refs.NextAsync(tenantId, "IND", ct);
        var indicator = QualityIndicator.Define(
            indicatorRef, command.Code, command.Name, command.Description,
            command.Numerator, command.Denominator, command.Unit, command.RateFactor,
            command.Frequency, command.Direction,
            command.Inclusions, command.Exclusions, command.DataSource);

        db.QualityIndicators.Add(indicator);
        await db.SaveChangesAsync(ct);
        return indicator.Id;
    }
}

// ── Update / targets / retire ────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Indicators, PermissionAction.Edit)]
public sealed record UpdateIndicatorDefinitionCommand(
    Guid IndicatorId, string Name, string? Description,
    string Numerator, string Denominator, string Unit, decimal RateFactor,
    IndicatorFrequency Frequency, IndicatorDirection Direction,
    string? Inclusions, string? Exclusions, string? DataSource)
    : ICommand;

// M-17: the update path carried none of Define's bounds — free text was
// unbounded and a zero rate factor would zero every future rate.
public sealed class UpdateIndicatorDefinitionValidator : AbstractValidator<UpdateIndicatorDefinitionCommand>
{
    public UpdateIndicatorDefinitionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Numerator).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Denominator).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RateFactor).GreaterThan(0m);
        RuleFor(x => x.Inclusions).MaximumLength(2000);
        RuleFor(x => x.Exclusions).MaximumLength(2000);
        RuleFor(x => x.DataSource).MaximumLength(1000);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Indicators, PermissionAction.Edit)]
public sealed record SetIndicatorTargetsCommand(
    Guid IndicatorId, decimal? Target, decimal? WarningThreshold, decimal? ActionThreshold) : ICommand;

[RequirePermissionPolicy(PermissionCatalog.Indicators, PermissionAction.Void)]
public sealed record RetireIndicatorCommand(Guid IndicatorId) : ICommand;

[RequirePermissionPolicy(PermissionCatalog.Indicators, PermissionAction.Create)]
public sealed record RecordMeasurementCommand(
    Guid IndicatorId, DateOnly Period, decimal Numerator, decimal Denominator, string? Note = null) : ICommand<Guid>;

public sealed class RecordMeasurementValidator : AbstractValidator<RecordMeasurementCommand>
{
    public RecordMeasurementValidator()
    {
        RuleFor(x => x.Denominator).GreaterThan(0m);
        RuleFor(x => x.Numerator).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

internal static class IndicatorLoader
{
    public static async Task<QualityIndicator> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.QualityIndicators
            .Include(i => i.Measurements)
            .SingleOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new DomainException("IND-404", "Indicator not found.");
}

public sealed class UpdateIndicatorDefinitionHandler(IAppDbContext db)
    : ICommandHandler<UpdateIndicatorDefinitionCommand>
{
    public async Task Handle(UpdateIndicatorDefinitionCommand c, CancellationToken ct)
    {
        var indicator = await IndicatorLoader.LoadAsync(db, c.IndicatorId, ct);
        indicator.UpdateDefinition(
            c.Name, c.Description, c.Numerator, c.Denominator, c.Unit, c.RateFactor,
            c.Frequency, c.Direction, c.Inclusions, c.Exclusions, c.DataSource);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SetIndicatorTargetsHandler(IAppDbContext db) : ICommandHandler<SetIndicatorTargetsCommand>
{
    public async Task Handle(SetIndicatorTargetsCommand c, CancellationToken ct)
    {
        var indicator = await IndicatorLoader.LoadAsync(db, c.IndicatorId, ct);
        indicator.SetTargets(c.Target, c.WarningThreshold, c.ActionThreshold);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RetireIndicatorHandler(IAppDbContext db) : ICommandHandler<RetireIndicatorCommand>
{
    public async Task Handle(RetireIndicatorCommand c, CancellationToken ct)
    {
        (await IndicatorLoader.LoadAsync(db, c.IndicatorId, ct)).Retire();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RecordMeasurementHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<RecordMeasurementCommand, Guid>
{
    public async Task<Guid> Handle(RecordMeasurementCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var indicator = await IndicatorLoader.LoadAsync(db, c.IndicatorId, ct);
        var id = indicator.RecordMeasurement(c.Period, c.Numerator, c.Denominator, actor, clock.UtcNow, c.Note);
        await db.SaveChangesAsync(ct);
        return id;
    }
}
