using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.MortalityReview;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.MortalityReview;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.MortalityReview;

// ── Mortality-review commands ───────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Create)]
public sealed record ReportMortalityCommand(
    string PatientRef, string Unit, DateTimeOffset DeathDateUtc, string? PrimaryDiagnosis, Guid? DepartmentId)
    : ICommand<Guid>;

public sealed class ReportMortalityValidator : AbstractValidator<ReportMortalityCommand>
{
    public ReportMortalityValidator()
    {
        RuleFor(x => x.PatientRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(100);
        RuleFor(x => x.PrimaryDiagnosis).MaximumLength(300);
    }
}

public sealed class ReportMortalityHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ReportMortalityCommand, Guid>
{
    public async Task<Guid> Handle(ReportMortalityCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var reviewRef = await refs.NextAsync(tenantId, "MRT", ct);
        var review = Domain.MortalityReview.MortalityReview.Report(
            reviewRef, c.PatientRef, c.Unit, c.DeathDateUtc, c.PrimaryDiagnosis, c.DepartmentId);
        db.MortalityReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Edit)]
public sealed record ClassifyMortalityCommand(Guid ReviewId, DeathClassification Classification, string Findings) : ICommand;

public sealed class ClassifyMortalityValidator : AbstractValidator<ClassifyMortalityCommand>
{
    public ClassifyMortalityValidator() => RuleFor(x => x.Findings).NotEmpty().MaximumLength(4000);
}

public sealed class ClassifyMortalityHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<ClassifyMortalityCommand>
{
    public async Task Handle(ClassifyMortalityCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var review = await Load(db, c.ReviewId, ct);
        review.Classify(actor, c.Classification, c.Findings);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<Domain.MortalityReview.MortalityReview> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.MortalityReviews.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("MRT-404", "Mortality review not found.");
}

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Approve)]
public sealed record RecordSecondReviewCommand(Guid ReviewId, string Notes, bool Concurs) : ICommand;

public sealed class RecordSecondReviewValidator : AbstractValidator<RecordSecondReviewCommand>
{
    public RecordSecondReviewValidator() => RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
}

public sealed class RecordSecondReviewHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<RecordSecondReviewCommand>
{
    public async Task Handle(RecordSecondReviewCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var review = await ClassifyMortalityHandler.Load(db, c.ReviewId, ct);
        review.RecordSecondReview(actor, c.Notes, c.Concurs);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Edit)]
public sealed record MarkCommitteeDiscussedCommand(Guid ReviewId, string Learnings) : ICommand;

public sealed class MarkCommitteeDiscussedValidator : AbstractValidator<MarkCommitteeDiscussedCommand>
{
    public MarkCommitteeDiscussedValidator() => RuleFor(x => x.Learnings).NotEmpty().MaximumLength(4000);
}

public sealed class MarkCommitteeDiscussedHandler(IAppDbContext db) : ICommandHandler<MarkCommitteeDiscussedCommand>
{
    public async Task Handle(MarkCommitteeDiscussedCommand c, CancellationToken ct)
    {
        (await ClassifyMortalityHandler.Load(db, c.ReviewId, ct)).MarkCommitteeDiscussed(c.Learnings);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Void)]
public sealed record CloseMortalityCommand(Guid ReviewId) : ICommand;

public sealed class CloseMortalityHandler(IAppDbContext db) : ICommandHandler<CloseMortalityCommand>
{
    public async Task Handle(CloseMortalityCommand c, CancellationToken ct)
    {
        (await ClassifyMortalityHandler.Load(db, c.ReviewId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

// ── Complication-register commands ──────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Create)]
public sealed record ReportComplicationCommand(
    string PatientRef, string Unit, ComplicationType Type, ComplicationSeverity Severity,
    DateTimeOffset OccurredDateUtc, string Description, Guid? DepartmentId) : ICommand<Guid>;

public sealed class ReportComplicationValidator : AbstractValidator<ReportComplicationCommand>
{
    public ReportComplicationValidator()
    {
        RuleFor(x => x.PatientRef).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public sealed class ReportComplicationHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ReportComplicationCommand, Guid>
{
    public async Task<Guid> Handle(ReportComplicationCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var caseRef = await refs.NextAsync(tenantId, "CMP", ct);
        var complication = ComplicationCase.Report(
            caseRef, c.PatientRef, c.Unit, c.Type, c.Severity, c.OccurredDateUtc, c.Description, c.DepartmentId);
        db.ComplicationCases.Add(complication);
        await db.SaveChangesAsync(ct);
        return complication.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Edit)]
public sealed record ReviewComplicationCommand(Guid CaseId, string Notes, bool Preventable) : ICommand;

public sealed class ReviewComplicationValidator : AbstractValidator<ReviewComplicationCommand>
{
    public ReviewComplicationValidator() => RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
}

public sealed class ReviewComplicationHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<ReviewComplicationCommand>
{
    public async Task Handle(ReviewComplicationCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var complication = await LoadComplication(db, c.CaseId, ct);
        complication.RecordReview(actor, c.Notes, c.Preventable, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<ComplicationCase> LoadComplication(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.ComplicationCases.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("CMP-404", "Complication case not found.");
}

[RequirePermissionPolicy(PermissionCatalog.MortalityReview, PermissionAction.Void)]
public sealed record CloseComplicationCommand(Guid CaseId) : ICommand;

public sealed class CloseComplicationHandler(IAppDbContext db) : ICommandHandler<CloseComplicationCommand>
{
    public async Task Handle(CloseComplicationCommand c, CancellationToken ct)
    {
        (await ReviewComplicationHandler.LoadComplication(db, c.CaseId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetMortalityReviewsQuery(string? Classification = null, string? Status = null)
    : IQuery<IReadOnlyList<MortalityListItemDto>>;

public sealed class GetMortalityReviewsHandler(IAppDbContext db)
    : IQueryHandler<GetMortalityReviewsQuery, IReadOnlyList<MortalityListItemDto>>
{
    public async Task<IReadOnlyList<MortalityListItemDto>> Handle(GetMortalityReviewsQuery q, CancellationToken ct)
    {
        var query = db.MortalityReviews.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Classification))
        {
            query = query.Where(m => m.Classification != null && m.Classification.ToString() == q.Classification);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(m => m.Status.ToString() == q.Status);
        }

        return await query
            .OrderByDescending(m => m.DeathDateUtc)
            .Select(m => new MortalityListItemDto(
                m.Id, m.ReviewRef, m.PatientRef, m.Unit, m.DeathDateUtc,
                m.Classification != null ? m.Classification.ToString() : null, m.RequiresSecondReview, m.Status.ToString()))
            .ToListAsync(ct);
    }
}

public sealed record GetMortalityByIdQuery(Guid ReviewId) : IQuery<MortalityDetailDto>;

public sealed class GetMortalityByIdHandler(IAppDbContext db) : IQueryHandler<GetMortalityByIdQuery, MortalityDetailDto>
{
    public async Task<MortalityDetailDto> Handle(GetMortalityByIdQuery q, CancellationToken ct)
    {
        var m = await db.MortalityReviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.ReviewId, ct)
            ?? throw new DomainException("MRT-404", "Mortality review not found.");

        return new MortalityDetailDto(
            m.Id, m.ReviewRef, m.PatientRef, m.Unit, m.DepartmentId, m.DeathDateUtc, m.PrimaryDiagnosis, m.Status.ToString(),
            m.Classification != null ? m.Classification.ToString() : null, m.RequiresSecondReview,
            m.FirstReviewerId, m.ClassificationFindings,
            m.SecondReviewerId, m.SecondReviewNotes, m.SecondReviewerConcurs, m.CommitteeLearnings);
    }
}

public sealed record GetComplicationsQuery(string? Type = null, string? Status = null)
    : IQuery<IReadOnlyList<ComplicationListItemDto>>;

public sealed class GetComplicationsHandler(IAppDbContext db)
    : IQueryHandler<GetComplicationsQuery, IReadOnlyList<ComplicationListItemDto>>
{
    public async Task<IReadOnlyList<ComplicationListItemDto>> Handle(GetComplicationsQuery q, CancellationToken ct)
    {
        var query = db.ComplicationCases.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Type))
        {
            query = query.Where(x => x.Type.ToString() == q.Type);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(x => x.Status.ToString() == q.Status);
        }

        return await query
            .OrderByDescending(x => x.OccurredDateUtc)
            .Select(x => new ComplicationListItemDto(
                x.Id, x.CaseRef, x.PatientRef, x.Unit, x.Type.ToString(), x.Severity.ToString(),
                x.OccurredDateUtc, x.Preventable, x.Status.ToString()))
            .ToListAsync(ct);
    }
}

public sealed record GetComplicationByIdQuery(Guid CaseId) : IQuery<ComplicationDetailDto>;

public sealed class GetComplicationByIdHandler(IAppDbContext db) : IQueryHandler<GetComplicationByIdQuery, ComplicationDetailDto>
{
    public async Task<ComplicationDetailDto> Handle(GetComplicationByIdQuery q, CancellationToken ct)
    {
        var x = await db.ComplicationCases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == q.CaseId, ct)
            ?? throw new DomainException("CMP-404", "Complication case not found.");

        return new ComplicationDetailDto(
            x.Id, x.CaseRef, x.PatientRef, x.Unit, x.DepartmentId, x.Type.ToString(), x.Severity.ToString(),
            x.OccurredDateUtc, x.Description, x.Status.ToString(), x.ReviewedBy, x.ReviewNotes, x.Preventable, x.ReviewedAtUtc);
    }
}

/// <summary>
/// Mortality &amp; morbidity rates over a window (HQMS M10). The mortality rate is per 1,000
/// patient-days, using the M24 ADT-derived denominator (the same loop as patient-safety and IPC),
/// with the peer-review classification breakdown and the complication counts.
/// </summary>
public sealed record GetMortalityRatesQuery(int WindowDays = 30) : IQuery<MortalityRatesDto>;

public sealed class GetMortalityRatesHandler(IAppDbContext db, IClock clock) : IQueryHandler<GetMortalityRatesQuery, MortalityRatesDto>
{
    public async Task<MortalityRatesDto> Handle(GetMortalityRatesQuery q, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var from = now.AddDays(-Math.Clamp(q.WindowDays, 1, 366));

        // Denominator: patient-days from the M24 ADT projection, clipped to the window.
        var stays = await db.PatientStays.AsNoTracking()
            .Where(s => s.DischargedAtUtc == null || s.DischargedAtUtc >= from)
            .Select(s => new { s.AdmittedAtUtc, s.DischargedAtUtc })
            .ToListAsync(ct);
        var patientDays = stays.Sum(s => WindowedDays.Clipped(s.AdmittedAtUtc, s.DischargedAtUtc, from, now));

        var deaths = await db.MortalityReviews.AsNoTracking()
            .Where(m => m.DeathDateUtc >= from && m.DeathDateUtc <= now)
            .Select(m => m.Classification)
            .ToListAsync(ct);

        var complications = await db.ComplicationCases.AsNoTracking()
            .Where(x => x.OccurredDateUtc >= from && x.OccurredDateUtc <= now)
            .Select(x => x.Preventable)
            .ToListAsync(ct);

        int CountClass(DeathClassification k) => deaths.Count(c => c == k);

        return new MortalityRatesDto(
            from, now, patientDays,
            deaths.Count, patientDays == 0 ? 0m : decimal.Round(deaths.Count * 1000m / patientDays, 2),
            CountClass(DeathClassification.Expected), CountClass(DeathClassification.Unexpected),
            CountClass(DeathClassification.PotentiallyPreventable), CountClass(DeathClassification.Preventable),
            complications.Count, complications.Count(p => p == true));
    }
}
