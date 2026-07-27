using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IdentityAccess.Commands;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record OpenAccessReviewCommand : ICommand<Guid>;
[RequireInternalActor]
public sealed record CompleteAccessReviewCommand(
    Guid ReviewId, bool ChangesRequired, string Conclusion) : ICommand;

public sealed class CompleteAccessReviewValidator : AbstractValidator<CompleteAccessReviewCommand>
{
    public CompleteAccessReviewValidator() =>
        RuleFor(x => x.Conclusion).NotEmpty().MaximumLength(4000);
}

public sealed class OpenAccessReviewHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs, IClock clock)
    : ICommandHandler<OpenAccessReviewCommand, Guid>
{
    public async Task<Guid> Handle(OpenAccessReviewCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var reviewRef = await refs.NextAsync(tenantId, "UAR", ct);
        var review = UserAccessReview.Open(reviewRef, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        db.UserAccessReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review.Id;
    }
}

public sealed class CompleteAccessReviewHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IClock clock)
    : ICommandHandler<CompleteAccessReviewCommand>
{
    public async Task Handle(CompleteAccessReviewCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var review = await db.UserAccessReviews.FirstOrDefaultAsync(r => r.Id == c.ReviewId, ct)
            ?? throw new DomainException("UAR-404", "Access review not found.");

        // Coverage evidence: the active accounts in this tenant, counted at completion.
        var accounts = await db.Users
            .CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);

        review.Complete(actor, clock.UtcNow, accounts, c.ChangesRequired, c.Conclusion);
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Query â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetAccessReviewsQuery : IQuery<IReadOnlyList<UserAccessReviewDto>>;

public sealed class GetAccessReviewsHandler(IAppDbContext db)
    : IQueryHandler<GetAccessReviewsQuery, IReadOnlyList<UserAccessReviewDto>>
{
    public async Task<IReadOnlyList<UserAccessReviewDto>> Handle(GetAccessReviewsQuery q, CancellationToken ct) =>
        await db.UserAccessReviews.AsNoTracking()
            .OrderByDescending(r => r.OpenedOn)
            .Select(r => new UserAccessReviewDto(
                r.Id, r.ReviewRef, r.OpenedOn, r.Status.ToString(),
                r.ReviewedBy, r.CompletedAtUtc, r.AccountsReviewed, r.ChangesRequired, r.Conclusion))
            .ToListAsync(ct);
}
