using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Compliance;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.ComplianceLedger;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record OpenAuditTrailReviewCommand(DateOnly PeriodStart, DateOnly PeriodEnd) : ICommand<Guid>;
public sealed record CompleteAuditTrailReviewCommand(
    Guid ReviewId, bool AnomaliesFound, string Conclusion) : ICommand;

public sealed class CompleteAuditTrailReviewValidator : AbstractValidator<CompleteAuditTrailReviewCommand>
{
    public CompleteAuditTrailReviewValidator()
    {
        RuleFor(x => x.Conclusion).NotEmpty().MaximumLength(4000);
    }
}

public sealed class OpenAuditTrailReviewHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<OpenAuditTrailReviewCommand, Guid>
{
    public async Task<Guid> Handle(OpenAuditTrailReviewCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var reviewRef = await refs.NextAsync(tenantId, "ATR", ct);
        var review = AuditTrailReview.Open(reviewRef, c.PeriodStart, c.PeriodEnd);
        db.AuditTrailReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review.Id;
    }
}

public sealed class CompleteAuditTrailReviewHandler(
    IAppDbContext db, IComplianceLedgerStore ledger, ICurrentTenant tenant, ICurrentUser user, IClock clock)
    : ICommandHandler<CompleteAuditTrailReviewCommand>
{
    public async Task Handle(CompleteAuditTrailReviewCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var review = await db.AuditTrailReviews.FirstOrDefaultAsync(r => r.Id == c.ReviewId, ct)
            ?? throw new DomainException("ATR-404", "Audit-trail review not found.");

        // Coverage evidence: the ledger volumes for the period, counted at completion.
        var fromUtc = new DateTimeOffset(review.PeriodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(review.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var (events, fieldChanges) = await ledger.CountForPeriodAsync(tenantId, fromUtc, toUtc, ct);

        review.Complete(actor, clock.UtcNow, events, fieldChanges, c.AnomaliesFound, c.Conclusion);
        await db.SaveChangesAsync(ct);
    }
}

// ── Anomaly saga ─────────────────────────────────────────────────────────────

/// <summary>
/// Data-integrity saga: an anomaly found during the periodic audit-trail
/// review is itself a quality incident, so it opens an NC for investigation.
/// Idempotent by SourceRef "ATR:{reviewId}".
/// </summary>
public sealed partial class AuditTrailAnomalyToNcPolicy(
    IAppDbContext db,
    ICurrentTenantSetter tenantSetter,
    IReferenceNumberGenerator refs,
    ILogger<AuditTrailAnomalyToNcPolicy> logger)
    : INotificationHandler<DomainEventNotification<AuditTrailAnomalyFound>>
{
    public async Task Handle(DomainEventNotification<AuditTrailAnomalyFound> notification, CancellationToken ct)
    {
        var e = notification.Event;
        tenantSetter.Set(e.TenantId);

        var sourceRef = $"ATR:{e.ReviewId}";
        if (await db.Nonconformances.AnyAsync(n => n.SourceRef == sourceRef, ct))
        {
            return; // Outbox redelivery — the NC already exists.
        }

        var ncRef = await refs.NextAsync(e.TenantId, "NC", ct);
        var nc = Nonconformance.Raise(
            ncRef,
            $"Audit-trail anomaly — review {e.ReviewRef} ({e.PeriodStart:yyyy-MM-dd} to {e.PeriodEnd:yyyy-MM-dd})",
            $"The periodic audit-trail review concluded: {e.Conclusion} " +
            "Investigate the anomaly as a potential data-integrity incident (21 CFR Part 11 §11.10(e)).",
            severity: 5,
            likelihood: 2,
            NcSourceType.Internal,
            e.ReviewedBy,
            sourceRef);
        nc.TenantId = e.TenantId;
        nc.Submit();

        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync(ct);
        LogNcRaised(logger, nc.NcRef, e.ReviewRef);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Raised {NcRef} for audit-trail anomaly in {ReviewRef}")]
    private static partial void LogNcRaised(ILogger logger, string ncRef, string reviewRef);
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetAuditTrailReviewsQuery : IQuery<IReadOnlyList<AuditTrailReviewDto>>;

public sealed class GetAuditTrailReviewsHandler(IAppDbContext db)
    : IQueryHandler<GetAuditTrailReviewsQuery, IReadOnlyList<AuditTrailReviewDto>>
{
    public async Task<IReadOnlyList<AuditTrailReviewDto>> Handle(GetAuditTrailReviewsQuery q, CancellationToken ct) =>
        await db.AuditTrailReviews.AsNoTracking()
            .OrderByDescending(r => r.PeriodEnd)
            .Select(r => new AuditTrailReviewDto(
                r.Id, r.ReviewRef, r.PeriodStart, r.PeriodEnd, r.Status.ToString(),
                r.ReviewedBy, r.CompletedAtUtc, r.EventsReviewed, r.FieldChangesReviewed,
                r.AnomaliesFound, r.Conclusion))
            .ToListAsync(ct);
}
