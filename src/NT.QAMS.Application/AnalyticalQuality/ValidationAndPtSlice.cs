using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.AnalyticalQuality;

// ── Method validation ────────────────────────────────────────────────────────

public sealed record ConfigureStudyCommand(string Analyte, string Protocol, decimal TotalAllowableError)
    : ICommand<Guid>;

public sealed class ConfigureStudyValidator : AbstractValidator<ConfigureStudyCommand>
{
    public ConfigureStudyValidator()
    {
        RuleFor(x => x.Analyte).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Protocol).NotEmpty().MaximumLength(30);
        RuleFor(x => x.TotalAllowableError).GreaterThan(0m);
    }
}

public sealed class ConfigureStudyHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ConfigureStudyCommand, Guid>
{
    public async Task<Guid> Handle(ConfigureStudyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var studyRef = await refs.NextAsync(tenantId, "MV", ct);
        var study = ValidationStudy.Configure(studyRef, c.Analyte, c.Protocol, c.TotalAllowableError);
        db.ValidationStudies.Add(study);
        await db.SaveChangesAsync(ct);
        return study.Id;
    }
}

public sealed record EnterReplicateCommand(Guid StudyId, string Level, decimal Measured, decimal? Reference)
    : ICommand;
public sealed record CalculateStudyCommand(Guid StudyId) : ICommand;
public sealed record SignOffStudyCommand(Guid StudyId) : ICommand;

internal static class StudyLoader
{
    public static async Task<ValidationStudy> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.ValidationStudies.Include(s => s.Replicates).SingleOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new DomainException("MV-404", "Validation study not found.");
}

public sealed class EnterReplicateHandler(IAppDbContext db) : ICommandHandler<EnterReplicateCommand>
{
    public async Task Handle(EnterReplicateCommand c, CancellationToken ct)
    {
        (await StudyLoader.LoadAsync(db, c.StudyId, ct)).EnterReplicate(c.Level, c.Measured, c.Reference);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CalculateStudyHandler(IAppDbContext db) : ICommandHandler<CalculateStudyCommand>
{
    public async Task Handle(CalculateStudyCommand c, CancellationToken ct)
    {
        (await StudyLoader.LoadAsync(db, c.StudyId, ct)).CalculateStatistics();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SignOffStudyHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<SignOffStudyCommand>
{
    public async Task Handle(SignOffStudyCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        (await StudyLoader.LoadAsync(db, c.StudyId, ct)).SignOff(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetStudiesQuery(string? State = null) : IQuery<IReadOnlyList<ValidationStudyListItemDto>>;

public sealed class GetStudiesHandler(IAppDbContext db)
    : IQueryHandler<GetStudiesQuery, IReadOnlyList<ValidationStudyListItemDto>>
{
    public async Task<IReadOnlyList<ValidationStudyListItemDto>> Handle(GetStudiesQuery q, CancellationToken ct)
    {
        var query = db.ValidationStudies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.State))
        {
            query = query.Where(s => s.State.ToString() == q.State);
        }

        return await query.OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new ValidationStudyListItemDto(
                s.Id, s.StudyRef, s.Analyte, s.Protocol, s.State.ToString(), s.Passed))
            .ToListAsync(ct);
    }
}

public sealed record GetStudyByIdQuery(Guid StudyId) : IQuery<ValidationStudyDetailDto>;

public sealed class GetStudyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetStudyByIdQuery, ValidationStudyDetailDto>
{
    public async Task<ValidationStudyDetailDto> Handle(GetStudyByIdQuery q, CancellationToken ct)
    {
        var s = await db.ValidationStudies.AsNoTracking().Include(x => x.Replicates)
            .SingleOrDefaultAsync(x => x.Id == q.StudyId, ct)
            ?? throw new DomainException("MV-404", "Validation study not found.");

        return new ValidationStudyDetailDto(
            s.Id, s.StudyRef, s.Analyte, s.Protocol, s.TotalAllowableError, s.State.ToString(),
            s.MeanBias, s.Cv, s.Passed, s.SignedOffBy, s.SignedOffAtUtc,
            s.Replicates.Select(r => new ReplicateDto(r.Id, r.Level, r.Measured, r.Reference)).ToList());
    }
}

// ── Proficiency testing ──────────────────────────────────────────────────────

public sealed record EnrollPtCommand(string Scheme, string Analyte, string Cycle) : ICommand<Guid>;

public sealed class EnrollPtHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<EnrollPtCommand, Guid>
{
    public async Task<Guid> Handle(EnrollPtCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var ptRef = await refs.NextAsync(tenantId, "PT", ct);
        var enrollment = PtEnrollment.Enroll(ptRef, c.Scheme, c.Analyte, c.Cycle);
        db.PtEnrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);
        return enrollment.Id;
    }
}

public sealed record RecordPtResultCommand(Guid PtId, decimal Submitted, decimal Assigned, decimal Sd)
    : ICommand;

public sealed class RecordPtResultHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<RecordPtResultCommand>
{
    public async Task Handle(RecordPtResultCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var enrollment = await db.PtEnrollments.SingleOrDefaultAsync(p => p.Id == c.PtId, ct)
            ?? throw new DomainException("PT-404", "PT enrollment not found.");
        enrollment.RecordResult(c.Submitted, c.Assigned, c.Sd, actor);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetPtEnrollmentsQuery(string? Performance = null) : IQuery<IReadOnlyList<PtEnrollmentDto>>;

public sealed class GetPtEnrollmentsHandler(IAppDbContext db)
    : IQueryHandler<GetPtEnrollmentsQuery, IReadOnlyList<PtEnrollmentDto>>
{
    public async Task<IReadOnlyList<PtEnrollmentDto>> Handle(GetPtEnrollmentsQuery q, CancellationToken ct)
    {
        var query = db.PtEnrollments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Performance))
        {
            query = query.Where(p => p.Performance.ToString() == q.Performance);
        }

        return await query.OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new PtEnrollmentDto(
                p.Id, p.PtRef, p.Scheme, p.Analyte, p.Cycle,
                p.SubmittedValue, p.AssignedValue, p.ZScore, p.Performance.ToString()))
            .ToListAsync(ct);
    }
}
