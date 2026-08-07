using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Compliance;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IdentityAccess.Commands;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record OpenAccessReviewCommand : ICommand<Guid>;
/// <summary>Completing a periodic user-access review is a Part 11 signing ceremony: it requires the reviewer's e-signature (account password + PIN).</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.AccessReviews,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record CompleteAccessReviewCommand(
    Guid ReviewId, bool ChangesRequired, string Conclusion, string Password, string Pin) : ICommand;

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
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IClock clock, IESignatureService signatures)
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

        // Pre-validate before minting (append-only ledger; mirrors the pilot). The aggregate re-checks.
        if (review.Status != UserAccessReviewStatus.Open)
        {
            throw new InvalidStateTransitionException(
                "UAR-010", "The access review is already completed and immutable.");
        }

        // Coverage evidence: the active accounts in this tenant, counted at completion.
        var accounts = await db.Users
            .CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);

        // Bind the signature to the determination being attested (§11.70), then mint before completing.
        await signatures.SignAsync(
            actor, c.Password, c.Pin,
            $"Completed access review {review.ReviewRef}: {(c.ChangesRequired ? "changes required" : "no changes")}",
            $"UAR:{review.Id:N}",
            SignatureContentHash.Compute(
                ("review", review.ReviewRef),
                ("outcome", c.ChangesRequired ? "changes-required" : "no-changes"),
                ("conclusion", c.Conclusion)), ct);

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
