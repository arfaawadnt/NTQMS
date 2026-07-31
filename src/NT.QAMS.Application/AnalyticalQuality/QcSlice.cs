using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

[RequireInternalActor]
public sealed record CreateQcProfileCommand(
    string Analyte, string Instrument, string ControlLot, decimal TargetMean, decimal TargetSd)
    : ICommand<Guid>;

public sealed class CreateQcProfileValidator : AbstractValidator<CreateQcProfileCommand>
{
    public CreateQcProfileValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Instrument).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TargetSd).GreaterThan(0m);
    }
}

public sealed class CreateQcProfileHandler(IAppDbContext db) : ICommandHandler<CreateQcProfileCommand, Guid>
{
    public async Task<Guid> Handle(CreateQcProfileCommand c, CancellationToken ct)
    {
        var profile = QcProfile.Create(c.Analyte, c.Instrument, c.ControlLot, c.TargetMean, c.TargetSd);
        db.QcProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile.Id;
    }
}

/// <summary>
/// Records a control run. Loads the profile targets + a recent window of prior
/// runs, evaluates Westgard rules once, and stores the verdict as the record of
/// fact. Out-of-control runs raise QcOutOfControl through the outbox.
/// </summary>
[RequireInternalActor]
public sealed record RecordQcRunCommand(Guid ProfileId, decimal Value, string Operator) : ICommand<Guid>;

public sealed class RecordQcRunHandler(IAppDbContext db, IClock clock, WestgardLimits westgardLimits)
    : ICommandHandler<RecordQcRunCommand, Guid>
{
    public const int WindowSize = 12;

    public async Task<Guid> Handle(RecordQcRunCommand c, CancellationToken ct)
    {
        var profile = await db.QcProfiles.SingleOrDefaultAsync(p => p.Id == c.ProfileId, ct)
            ?? throw new DomainException("QC-404", "QC profile not found.");

        var priorValues = await db.QcRuns
            .Where(r => r.ProfileId == c.ProfileId)
            .OrderByDescending(r => r.MeasuredAtUtc)
            .Take(WindowSize)
            .Select(r => r.Value)
            .ToListAsync(ct);
        priorValues.Reverse(); // oldest first for the evaluator

        var verdict = WestgardEvaluator.Evaluate(
            c.Value, profile.TargetMean, profile.TargetSd, priorValues, westgardLimits);
        var z = (c.Value - profile.TargetMean) / profile.TargetSd;

        var run = QcRun.Record(c.ProfileId, c.Value, z, verdict, c.Operator, clock.UtcNow);
        db.QcRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run.Id;
    }
}

[RequireInternalActor]
public sealed record UpdateQcTargetsCommand(Guid ProfileId, decimal TargetMean, decimal TargetSd, string Reason)
    : ICommand;

public sealed class UpdateQcTargetsValidator : AbstractValidator<UpdateQcTargetsCommand>
{
    public UpdateQcTargetsValidator()
    {
        RuleFor(x => x.TargetSd).GreaterThan(0m);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class UpdateQcTargetsHandler(IAppDbContext db, IClock clock)
    : ICommandHandler<UpdateQcTargetsCommand>
{
    public async Task Handle(UpdateQcTargetsCommand c, CancellationToken ct)
    {
        var profile = await db.QcProfiles.SingleOrDefaultAsync(p => p.Id == c.ProfileId, ct)
            ?? throw new DomainException("QC-404", "QC profile not found.");
        profile.UpdateTargets(c.TargetMean, c.TargetSd, c.Reason, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

[RequireInternalActor]
public sealed record LogQcTroubleshootingCommand(Guid RunId, string Note) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class LogQcTroubleshootingValidator : AbstractValidator<LogQcTroubleshootingCommand>
{
    public LogQcTroubleshootingValidator()
    {
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
    }
}

public sealed class LogQcTroubleshootingHandler(IAppDbContext db)
    : ICommandHandler<LogQcTroubleshootingCommand>
{
    public async Task Handle(LogQcTroubleshootingCommand c, CancellationToken ct)
    {
        var run = await db.QcRuns.SingleOrDefaultAsync(r => r.Id == c.RunId, ct)
            ?? throw new DomainException("QC-404", "QC run not found.");
        run.LogTroubleshooting(c.Note);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetQcProfilesQuery : IQuery<IReadOnlyList<QcProfileDto>>;

public sealed class GetQcProfilesHandler(IAppDbContext db)
    : IQueryHandler<GetQcProfilesQuery, IReadOnlyList<QcProfileDto>>
{
    public async Task<IReadOnlyList<QcProfileDto>> Handle(GetQcProfilesQuery q, CancellationToken ct) =>
        await db.QcProfiles.AsNoTracking().OrderBy(p => p.Analyte)
            .Select(p => new QcProfileDto(
                p.Id, p.Analyte, p.Instrument, p.ControlLot, p.TargetMean, p.TargetSd, p.IsActive))
            .ToListAsync(ct);
}

public sealed record GetQcRunsQuery(Guid ProfileId, int Take = 60) : IQuery<IReadOnlyList<QcRunDto>>;

public sealed class GetQcRunsHandler(IAppDbContext db)
    : IQueryHandler<GetQcRunsQuery, IReadOnlyList<QcRunDto>>
{
    public async Task<IReadOnlyList<QcRunDto>> Handle(GetQcRunsQuery q, CancellationToken ct) =>
        await db.QcRuns.AsNoTracking()
            .Where(r => r.ProfileId == q.ProfileId)
            .OrderByDescending(r => r.MeasuredAtUtc)
            .Take(Math.Clamp(q.Take, 1, 500))
            .Select(r => new QcRunDto(
                r.Id, r.ProfileId, r.Value, r.ZScore, r.Outcome, r.ViolatedRules,
                r.Operator, r.MeasuredAtUtc, r.TroubleshootingNote))
            .ToListAsync(ct);
}
