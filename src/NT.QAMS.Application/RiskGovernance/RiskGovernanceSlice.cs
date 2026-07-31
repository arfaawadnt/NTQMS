using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Governance;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.RiskGovernance;

internal static class GovernanceHelpers
{
    public static Guid RequireTenant(ICurrentTenant tenant) =>
        tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");

    public static Guid RequireActor(ICurrentUser user) =>
        user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
}

// â”€â”€ Risk â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record AssessRiskCommand(string Title, string Category, int Likelihood, int Impact,
    Guid? BranchId = null, Guid? DepartmentId = null)
    : ICommand<Guid>;

public sealed class AssessRiskValidator : AbstractValidator<AssessRiskCommand>
{
    public AssessRiskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Likelihood).InclusiveBetween(1, 5);
        RuleFor(x => x.Impact).InclusiveBetween(1, 5);
    }
}

public sealed class AssessRiskHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<AssessRiskCommand, Guid>
{
    public async Task<Guid> Handle(AssessRiskCommand c, CancellationToken ct)
    {
        var riskRef = await refs.NextAsync(GovernanceHelpers.RequireTenant(tenant), "RSK", ct);
        var risk = RiskItem.Assess(riskRef, c.Title, c.Category, c.Likelihood, c.Impact);
        risk.BranchId = c.BranchId;
        risk.DepartmentId = c.DepartmentId;
        db.Risks.Add(risk);
        await db.SaveChangesAsync(ct);
        return risk.Id;
    }
}

[RequireInternalActor]
public sealed record AddMitigationCommand(Guid RiskId, string Description, Guid OwnerId, DateOnly DueDate)
    : ICommand<Guid>;
[RequireInternalActor]
public sealed record CompleteMitigationCommand(Guid RiskId, Guid ActionId) : ICommand;
[RequireInternalActor]
public sealed record RecordResidualCommand(Guid RiskId, int Likelihood, int Impact) : ICommand;
[RequireInternalActor]
public sealed record CloseRiskCommand(Guid RiskId) : ICommand;

internal static class RiskLoader
{
    public static async Task<RiskItem> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Risks.Include(r => r.Actions).SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new DomainException("RSK-404", "Risk not found.");
}

public sealed class AddMitigationHandler(IAppDbContext db) : ICommandHandler<AddMitigationCommand, Guid>
{
    public async Task<Guid> Handle(AddMitigationCommand c, CancellationToken ct)
    {
        var risk = await RiskLoader.LoadAsync(db, c.RiskId, ct);
        var id = risk.AddMitigationAction(c.Description, c.OwnerId, c.DueDate);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

public sealed class CompleteMitigationHandler(IAppDbContext db) : ICommandHandler<CompleteMitigationCommand>
{
    public async Task Handle(CompleteMitigationCommand c, CancellationToken ct)
    {
        (await RiskLoader.LoadAsync(db, c.RiskId, ct)).CompleteMitigationAction(c.ActionId);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RecordResidualHandler(IAppDbContext db) : ICommandHandler<RecordResidualCommand>
{
    public async Task Handle(RecordResidualCommand c, CancellationToken ct)
    {
        (await RiskLoader.LoadAsync(db, c.RiskId, ct)).RecordResidualAssessment(c.Likelihood, c.Impact);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CloseRiskHandler(IAppDbContext db) : ICommandHandler<CloseRiskCommand>
{
    public async Task Handle(CloseRiskCommand c, CancellationToken ct)
    {
        (await RiskLoader.LoadAsync(db, c.RiskId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetRisksQuery(
    string? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<RiskListItemDto>>;

public sealed class GetRisksHandler(IAppDbContext db)
    : IQueryHandler<GetRisksQuery, Contracts.Common.PagedResponse<RiskListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<RiskListItemDto>> Handle(GetRisksQuery q, CancellationToken ct)
    {
        var query = db.Risks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(r => r.Status.ToString() == q.Status);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderByDescending(r => r.Rpn)
            .Select(r => new RiskListItemDto(
                r.Id, r.RiskRef, r.Title, r.Category, r.Status.ToString(), r.Rpn, r.ResidualRpn, r.BranchId, r.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetRiskByIdQuery(Guid RiskId) : IQuery<RiskDetailDto>;

public sealed class GetRiskByIdHandler(IAppDbContext db) : IQueryHandler<GetRiskByIdQuery, RiskDetailDto>
{
    public async Task<RiskDetailDto> Handle(GetRiskByIdQuery q, CancellationToken ct)
    {
        var r = await db.Risks.AsNoTracking().Include(x => x.Actions)
            .SingleOrDefaultAsync(x => x.Id == q.RiskId, ct)
            ?? throw new DomainException("RSK-404", "Risk not found.");

        return new RiskDetailDto(
            r.Id, r.RiskRef, r.Title, r.Category, r.Status.ToString(),
            r.Likelihood, r.Impact, r.Rpn,
            r.ResidualLikelihood, r.ResidualImpact, r.ResidualRpn,
            r.Actions.Select(a => new MitigationActionDto(
                a.Id, a.Description, a.OwnerId, a.DueDate, a.Completed)).ToList());
    }
}

// â”€â”€ Change control â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record ProposeChangeCommand(string Title, string ImpactAnalysis,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class ProposeChangeValidator : AbstractValidator<ProposeChangeCommand>
{
    public ProposeChangeValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ImpactAnalysis).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ProposeChangeHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs)
    : ICommandHandler<ProposeChangeCommand, Guid>
{
    public async Task<Guid> Handle(ProposeChangeCommand c, CancellationToken ct)
    {
        var changeRef = await refs.NextAsync(GovernanceHelpers.RequireTenant(tenant), "CHG", ct);
        var change = ChangeRequest.Propose(
            changeRef, c.Title, c.ImpactAnalysis, GovernanceHelpers.RequireActor(user));
        change.BranchId = c.BranchId;
        change.DepartmentId = c.DepartmentId;
        db.ChangeRequests.Add(change);
        await db.SaveChangesAsync(ct);
        return change.Id;
    }
}

[RequireInternalActor]
public sealed record LinkRiskCommand(Guid ChangeId, Guid RiskItemId) : ICommand;
[RequireInternalActor]
public sealed record ApproveChangeCommand(Guid ChangeId) : ICommand;
[RequireInternalActor]
public sealed record RejectChangeCommand(Guid ChangeId, string Reason) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class RejectChangeValidator : AbstractValidator<RejectChangeCommand>
{
    public RejectChangeValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
[RequireInternalActor]
public sealed record CloseChangeCommand(Guid ChangeId, string ImplementationNotes) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class CloseChangeValidator : AbstractValidator<CloseChangeCommand>
{
    public CloseChangeValidator()
    {
        RuleFor(x => x.ImplementationNotes).NotEmpty().MaximumLength(4000);
    }
}
[RequireInternalActor]
public sealed record ReviewChangeCommand(Guid ChangeId, bool Effective, string Notes) : ICommand;

public sealed class ReviewChangeValidator : AbstractValidator<ReviewChangeCommand>
{
    public ReviewChangeValidator() =>
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(4000);
}

internal static class ChangeLoader
{
    public static async Task<ChangeRequest> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.ChangeRequests.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("CHG-404", "Change request not found.");
}

public sealed class LinkRiskHandler(IAppDbContext db) : ICommandHandler<LinkRiskCommand>
{
    public async Task Handle(LinkRiskCommand c, CancellationToken ct)
    {
        if (!await db.Risks.AnyAsync(r => r.Id == c.RiskItemId, ct))
        {
            throw new DomainException("RSK-404", "Risk not found.");
        }

        (await ChangeLoader.LoadAsync(db, c.ChangeId, ct)).LinkRiskAssessment(c.RiskItemId);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ApproveChangeHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<ApproveChangeCommand>
{
    public async Task Handle(ApproveChangeCommand c, CancellationToken ct)
    {
        (await ChangeLoader.LoadAsync(db, c.ChangeId, ct))
            .Approve(GovernanceHelpers.RequireActor(user), clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RejectChangeHandler(IAppDbContext db) : ICommandHandler<RejectChangeCommand>
{
    public async Task Handle(RejectChangeCommand c, CancellationToken ct)
    {
        (await ChangeLoader.LoadAsync(db, c.ChangeId, ct)).Reject(c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CloseChangeHandler(IAppDbContext db) : ICommandHandler<CloseChangeCommand>
{
    public async Task Handle(CloseChangeCommand c, CancellationToken ct)
    {
        (await ChangeLoader.LoadAsync(db, c.ChangeId, ct)).Close(c.ImplementationNotes);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ReviewChangeHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<ReviewChangeCommand>
{
    public async Task Handle(ReviewChangeCommand c, CancellationToken ct)
    {
        (await ChangeLoader.LoadAsync(db, c.ChangeId, ct))
            .RecordPostImplementationReview(GovernanceHelpers.RequireActor(user), c.Effective, c.Notes, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetChangesQuery(
    string? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<ChangeListItemDto>>;

public sealed class GetChangesHandler(IAppDbContext db)
    : IQueryHandler<GetChangesQuery, Contracts.Common.PagedResponse<ChangeListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<ChangeListItemDto>> Handle(GetChangesQuery q, CancellationToken ct)
    {
        var query = db.ChangeRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(x => x.Status.ToString() == q.Status);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ChangeListItemDto(x.Id, x.ChangeRef, x.Title, x.Status.ToString(), x.RiskItemId, x.BranchId, x.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetChangeByIdQuery(Guid ChangeId) : IQuery<ChangeDetailDto>;

public sealed class GetChangeByIdHandler(IAppDbContext db)
    : IQueryHandler<GetChangeByIdQuery, ChangeDetailDto>
{
    public async Task<ChangeDetailDto> Handle(GetChangeByIdQuery q, CancellationToken ct)
    {
        var x = await db.ChangeRequests.AsNoTracking().SingleOrDefaultAsync(c => c.Id == q.ChangeId, ct)
            ?? throw new DomainException("CHG-404", "Change request not found.");

        return new ChangeDetailDto(
            x.Id, x.ChangeRef, x.Title, x.ImpactAnalysis, x.Status.ToString(),
            x.ProposedBy, x.RiskItemId, x.ApprovedBy, x.ApprovedAtUtc,
            x.RejectionReason, x.ImplementationNotes,
            x.ChangeEffective, x.PostImplementationReviewNotes,
            x.PostImplementationReviewedBy, x.PostImplementationReviewedAtUtc);
    }
}

// â”€â”€ Management review â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record ScheduleReviewCommand(string Title, DateOnly ReviewDate, string Participants,
    Guid? BranchId = null, Guid? DepartmentId = null)
    : ICommand<Guid>;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class ScheduleReviewValidator : AbstractValidator<ScheduleReviewCommand>
{
    public ScheduleReviewValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Participants).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ScheduleReviewHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ScheduleReviewCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleReviewCommand c, CancellationToken ct)
    {
        var reviewRef = await refs.NextAsync(GovernanceHelpers.RequireTenant(tenant), "MRV", ct);
        var review = ManagementReview.Schedule(reviewRef, c.Title, c.ReviewDate, c.Participants);
        review.BranchId = c.BranchId;
        review.DepartmentId = c.DepartmentId;
        db.ManagementReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review.Id;
    }
}

[RequireInternalActor]
public sealed record AddDecisionCommand(Guid ReviewId, string Description, Guid OwnerId, DateOnly DueDate)
    : ICommand<Guid>;
[RequireInternalActor]
public sealed record CloseReviewCommand(Guid ReviewId, string Minutes) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class CloseReviewValidator : AbstractValidator<CloseReviewCommand>
{
    public CloseReviewValidator()
    {
        RuleFor(x => x.Minutes).NotEmpty().MaximumLength(20000);
    }
}

public sealed class AddDecisionHandler(IAppDbContext db) : ICommandHandler<AddDecisionCommand, Guid>
{
    public async Task<Guid> Handle(AddDecisionCommand c, CancellationToken ct)
    {
        var review = await db.ManagementReviews.Include(r => r.Decisions)
            .SingleOrDefaultAsync(r => r.Id == c.ReviewId, ct)
            ?? throw new DomainException("MRV-404", "Management review not found.");
        var id = review.AddDecision(c.Description, c.OwnerId, c.DueDate);
        await db.SaveChangesAsync(ct);
        return id;
    }
}

public sealed class CloseReviewHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<CloseReviewCommand>
{
    public async Task Handle(CloseReviewCommand c, CancellationToken ct)
    {
        var review = await db.ManagementReviews.Include(r => r.Decisions)
            .SingleOrDefaultAsync(r => r.Id == c.ReviewId, ct)
            ?? throw new DomainException("MRV-404", "Management review not found.");
        review.Close(GovernanceHelpers.RequireActor(user), c.Minutes);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetReviewsQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<ReviewListItemDto>>;

public sealed class GetReviewsHandler(IAppDbContext db)
    : IQueryHandler<GetReviewsQuery, Contracts.Common.PagedResponse<ReviewListItemDto>>
{
    // API-004: pagination envelope — no silent cap; the client sees the total.
    public async Task<Contracts.Common.PagedResponse<ReviewListItemDto>> Handle(GetReviewsQuery q, CancellationToken ct) =>
        await db.ManagementReviews.AsNoTracking()
            .OrderByDescending(r => r.ReviewDate)
            .Select(r => new ReviewListItemDto(
                r.Id, r.ReviewRef, r.Title, r.ReviewDate, r.Status.ToString(), r.Decisions.Count, r.BranchId, r.DepartmentId))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
}

public sealed record GetReviewByIdQuery(Guid ReviewId) : IQuery<ReviewDetailDto>;

public sealed class GetReviewByIdHandler(IAppDbContext db)
    : IQueryHandler<GetReviewByIdQuery, ReviewDetailDto>
{
    public async Task<ReviewDetailDto> Handle(GetReviewByIdQuery q, CancellationToken ct)
    {
        var r = await db.ManagementReviews.AsNoTracking().Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == q.ReviewId, ct)
            ?? throw new DomainException("MRV-404", "Management review not found.");

        return new ReviewDetailDto(
            r.Id, r.ReviewRef, r.Title, r.ReviewDate, r.Participants, r.Status.ToString(),
            r.Minutes, r.ClosedBy,
            r.Decisions.Select(d => new ReviewDecisionDto(d.Id, d.Description, d.OwnerId, d.DueDate))
                .ToList());
    }
}
