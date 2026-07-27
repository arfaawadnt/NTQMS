using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Resources;
using NT.QAMS.Domain.Competency;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Competency;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record GrantTestAuthorizationCommand(
    Guid UserId, Guid TestCatalogItemId, Guid CompetencyRecordId, string Scope) : ICommand<Guid>;

public sealed class GrantTestAuthorizationValidator : AbstractValidator<GrantTestAuthorizationCommand>
{
    public GrantTestAuthorizationValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TestCatalogItemId).NotEmpty();
        RuleFor(x => x.CompetencyRecordId).NotEmpty();
        RuleFor(x => x.Scope).NotEmpty();
    }
}

public sealed class GrantTestAuthorizationHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<GrantTestAuthorizationCommand, Guid>
{
    public async Task<Guid> Handle(GrantTestAuthorizationCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var scope = Enum.Parse<AuthorizationScope>(c.Scope, ignoreCase: true);

        var test = await db.TestCatalogItems.FirstOrDefaultAsync(t => t.Id == c.TestCatalogItemId, ct)
            ?? throw new DomainException("ORG-404", "Catalog test not found.");
        if (!test.IsActive)
        {
            throw new DomainException("AUTHZ-002", $"Test {test.TestCode} is inactive â€” authorizations can only target active catalog tests.");
        }

        // The evidence gate: a current, Authorized competency belonging to the
        // same person. Its requalification date becomes the authorization expiry.
        var competency = await db.Competencies.FirstOrDefaultAsync(x => x.Id == c.CompetencyRecordId, ct)
            ?? throw new DomainException("COMP-404", "Competency record not found.");
        if (competency.TraineeId != c.UserId)
        {
            throw new DomainException("AUTHZ-003", "The evidencing competency belongs to a different person.");
        }

        if (competency.Status != CompetencyStatus.Authorized || competency.ExpiresAt is null)
        {
            throw new DomainException("AUTHZ-004", "Only a current, Authorized competency can evidence a test authorization.");
        }

        var duplicate = await db.TestAuthorizations.AnyAsync(a =>
            a.UserId == c.UserId && a.TestCatalogItemId == c.TestCatalogItemId && a.Scope == scope
            && (a.Status == TestAuthorizationStatus.Active || a.Status == TestAuthorizationStatus.Suspended), ct);
        if (duplicate)
        {
            throw new DomainException("AUTHZ-005", "An equivalent authorization (same person, test and scope) is already in force.");
        }

        var authorization = TestAuthorization.Grant(
            c.UserId, c.TestCatalogItemId, c.CompetencyRecordId, scope, actor,
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), competency.ExpiresAt.Value);
        db.TestAuthorizations.Add(authorization);
        await db.SaveChangesAsync(ct);
        return authorization.Id;
    }
}

[RequireInternalActor]
public sealed record SuspendTestAuthorizationCommand(Guid AuthorizationId, string Reason) : ICommand;
[RequireInternalActor]
public sealed record ReinstateTestAuthorizationCommand(Guid AuthorizationId) : ICommand;
[RequireInternalActor]
public sealed record RevokeTestAuthorizationCommand(Guid AuthorizationId, string Reason) : ICommand;

public sealed class SuspendTestAuthorizationValidator : AbstractValidator<SuspendTestAuthorizationCommand>
{
    public SuspendTestAuthorizationValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class RevokeTestAuthorizationValidator : AbstractValidator<RevokeTestAuthorizationCommand>
{
    public RevokeTestAuthorizationValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class TestAuthorizationWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<SuspendTestAuthorizationCommand>,
    ICommandHandler<ReinstateTestAuthorizationCommand>,
    ICommandHandler<RevokeTestAuthorizationCommand>
{
    public async Task Handle(SuspendTestAuthorizationCommand c, CancellationToken ct)
    {
        var authorization = await LoadAsync(c.AuthorizationId, ct);
        authorization.Suspend(c.Reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReinstateTestAuthorizationCommand c, CancellationToken ct)
    {
        var authorization = await LoadAsync(c.AuthorizationId, ct);
        authorization.Reinstate(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RevokeTestAuthorizationCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var authorization = await LoadAsync(c.AuthorizationId, ct);
        authorization.Revoke(actor, c.Reason);
        await db.SaveChangesAsync(ct);
    }

    private async Task<TestAuthorization> LoadAsync(Guid id, CancellationToken ct) =>
        await db.TestAuthorizations.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new DomainException("AUTHZ-404", "Test authorization not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetTestAuthorizationsQuery(Guid? UserId = null, string? Status = null)
    : IQuery<IReadOnlyList<TestAuthorizationListItemDto>>;

public sealed class GetTestAuthorizationsHandler(IAppDbContext db)
    : IQueryHandler<GetTestAuthorizationsQuery, IReadOnlyList<TestAuthorizationListItemDto>>
{
    public async Task<IReadOnlyList<TestAuthorizationListItemDto>> Handle(
        GetTestAuthorizationsQuery q, CancellationToken ct)
    {
        var query = db.TestAuthorizations.AsNoTracking();
        if (q.UserId is { } userId)
        {
            query = query.Where(a => a.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<TestAuthorizationStatus>(q.Status, ignoreCase: true, out var status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .Join(db.TestCatalogItems.AsNoTracking(),
                a => a.TestCatalogItemId, t => t.Id,
                (a, t) => new { a, t })
            .OrderBy(x => x.t.TestCode)
            .Select(x => new TestAuthorizationListItemDto(
                x.a.Id, x.a.UserId, x.a.TestCatalogItemId, x.t.TestCode, x.t.TestName,
                x.a.Scope.ToString(), x.a.Status.ToString(), x.a.GrantedOn, x.a.ExpiresOn))
            .ToListAsync(ct);
    }
}

public sealed record GetTestAuthorizationByIdQuery(Guid AuthorizationId) : IQuery<TestAuthorizationDetailDto>;

public sealed class GetTestAuthorizationByIdHandler(IAppDbContext db)
    : IQueryHandler<GetTestAuthorizationByIdQuery, TestAuthorizationDetailDto>
{
    public async Task<TestAuthorizationDetailDto> Handle(GetTestAuthorizationByIdQuery q, CancellationToken ct)
    {
        var a = await db.TestAuthorizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.AuthorizationId, ct)
            ?? throw new DomainException("AUTHZ-404", "Test authorization not found.");

        var test = await db.TestCatalogItems.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == a.TestCatalogItemId, ct);
        var competency = await db.Competencies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == a.CompetencyRecordId, ct);

        return new TestAuthorizationDetailDto(
            a.Id, a.UserId, a.TestCatalogItemId, test?.TestCode ?? "?", test?.TestName ?? "?",
            a.CompetencyRecordId, competency?.Subject, a.Scope.ToString(), a.Status.ToString(),
            a.GrantedBy, a.GrantedOn, a.ExpiresOn, a.SuspensionReason, a.RevocationReason);
    }
}
