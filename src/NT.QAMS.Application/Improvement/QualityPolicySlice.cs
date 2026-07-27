using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Improvement;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Improvement;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record DraftQualityPolicyCommand(string Statement) : ICommand<Guid>;
[RequireInternalActor]
public sealed record ReviseQualityPolicyCommand(Guid PolicyId, string Statement) : ICommand;
[RequireInternalActor]
public sealed record ApproveQualityPolicyCommand(Guid PolicyId, DateOnly EffectiveDate) : ICommand;

public sealed class DraftQualityPolicyValidator : AbstractValidator<DraftQualityPolicyCommand>
{
    public DraftQualityPolicyValidator() =>
        RuleFor(x => x.Statement).NotEmpty().MaximumLength(8000);
}

public sealed class ReviseQualityPolicyValidator : AbstractValidator<ReviseQualityPolicyCommand>
{
    public ReviseQualityPolicyValidator() =>
        RuleFor(x => x.Statement).NotEmpty().MaximumLength(8000);
}

public sealed class DraftQualityPolicyHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<DraftQualityPolicyCommand, Guid>
{
    public async Task<Guid> Handle(DraftQualityPolicyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        // Version is the next in this tenant's sequence â€” a new draft always builds on the latest.
        var latestVersion = await db.QualityPolicies
            .Where(p => p.TenantId == tenantId)
            .Select(p => (int?)p.Version)
            .OrderByDescending(v => v)
            .FirstOrDefaultAsync(ct) ?? 0;

        var policyRef = await refs.NextAsync(tenantId, "QP", ct);
        var policy = QualityPolicy.Draft(policyRef, latestVersion + 1, c.Statement);
        db.QualityPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy.Id;
    }
}

public sealed class QualityPolicyWorkflowHandlers(IAppDbContext db, ICurrentUser user, IClock clock) :
    ICommandHandler<ReviseQualityPolicyCommand>,
    ICommandHandler<ApproveQualityPolicyCommand>
{
    public async Task Handle(ReviseQualityPolicyCommand c, CancellationToken ct)
    {
        var policy = await LoadAsync(c.PolicyId, ct);
        policy.ReviseDraft(c.Statement);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ApproveQualityPolicyCommand c, CancellationToken ct)
    {
        var approver = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var policy = await LoadAsync(c.PolicyId, ct);

        // Only one policy is ever in force: retire the current active version first.
        var active = await db.QualityPolicies
            .Where(p => p.Status == QualityPolicyStatus.Active && p.Id != policy.Id)
            .ToListAsync(ct);
        foreach (var prior in active)
        {
            prior.Supersede();
        }

        policy.Approve(approver, clock.UtcNow, c.EffectiveDate);
        await db.SaveChangesAsync(ct);
    }

    private async Task<QualityPolicy> LoadAsync(Guid id, CancellationToken ct) =>
        await db.QualityPolicies.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new DomainException("QP-404", "Quality policy not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetQualityPoliciesQuery : IQuery<IReadOnlyList<QualityPolicyDto>>;

public sealed class GetQualityPoliciesHandler(IAppDbContext db)
    : IQueryHandler<GetQualityPoliciesQuery, IReadOnlyList<QualityPolicyDto>>
{
    public async Task<IReadOnlyList<QualityPolicyDto>> Handle(GetQualityPoliciesQuery q, CancellationToken ct) =>
        await db.QualityPolicies.AsNoTracking()
            .OrderByDescending(p => p.Version)
            .Select(p => new QualityPolicyDto(
                p.Id, p.PolicyRef, p.Version, p.Statement, p.Status.ToString(),
                p.EffectiveDate, p.ApprovedById, p.ApprovedAtUtc))
            .ToListAsync(ct);
}

/// <summary>The single policy currently in force (Active), or null if none has been approved yet.</summary>
public sealed record GetActiveQualityPolicyQuery : IQuery<QualityPolicyDto?>;

public sealed class GetActiveQualityPolicyHandler(IAppDbContext db)
    : IQueryHandler<GetActiveQualityPolicyQuery, QualityPolicyDto?>
{
    public async Task<QualityPolicyDto?> Handle(GetActiveQualityPolicyQuery q, CancellationToken ct) =>
        await db.QualityPolicies.AsNoTracking()
            .Where(p => p.Status == QualityPolicyStatus.Active)
            .Select(p => new QualityPolicyDto(
                p.Id, p.PolicyRef, p.Version, p.Statement, p.Status.ToString(),
                p.EffectiveDate, p.ApprovedById, p.ApprovedAtUtc))
            .FirstOrDefaultAsync(ct);
}
