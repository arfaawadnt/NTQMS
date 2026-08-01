using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Reporting;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Reporting;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Reporting;

// ── Read the weighting ───────────────────────────────────────────────────────

/// <summary>The tenant's Quality Health Score weighting, for the configuration screen.</summary>
[RequirePermissionPolicy(PermissionCatalog.Reports, PermissionAction.View)]
public sealed record GetQualityHealthProfileQuery : IQuery<QualityHealthProfileDto>;

/// <summary>
/// Returns the stored weighting, or the equal-weighted default when the tenant has
/// never tuned it. The default is reported as if stored so the screen and the
/// score always agree — the profile row is created on first edit, not on first read,
/// because a read should not write.
/// </summary>
public sealed class GetQualityHealthProfileHandler(IAppDbContext db)
    : IQueryHandler<GetQualityHealthProfileQuery, QualityHealthProfileDto>
{
    public async Task<QualityHealthProfileDto> Handle(
        GetQualityHealthProfileQuery query, CancellationToken ct)
    {
        var profile = await db.QualityHealthProfiles
            .AsNoTracking()
            .Include(p => p.Weights)
            .FirstOrDefaultAsync(ct);

        var weights = Enum.GetValues<QualityHealthCategory>()
            .Select(category => new QualityHealthWeightDto(
                category.ToString(),
                profile?.WeightFor(category) ?? QualityHealthProfile.DefaultWeight))
            .ToList();

        return new QualityHealthProfileDto(weights);
    }
}

// ── Change the weighting ─────────────────────────────────────────────────────

/// <summary>
/// Replaces the tenant's weighting. Gated on <c>reports.manage</c> rather than
/// <c>reports.view</c>: reading the analytics and redefining how the headline
/// score is calculated are different privileges.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.Reports, PermissionAction.Manage)]
public sealed record UpdateQualityHealthWeightsCommand(
    IReadOnlyList<QualityHealthWeightDto> Weights,
    string Reason) : ICommand;

public sealed class UpdateQualityHealthWeightsValidator
    : AbstractValidator<UpdateQualityHealthWeightsCommand>
{
    public UpdateQualityHealthWeightsValidator()
    {
        RuleFor(c => c.Weights).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>
/// Applies the new weighting through the aggregate, so the range, completeness and
/// all-zero invariants are enforced in one place and the change is raised as a
/// domain event — the audit trail records what the score meant before and after.
/// </summary>
public sealed class UpdateQualityHealthWeightsHandler(IAppDbContext db)
    : ICommandHandler<UpdateQualityHealthWeightsCommand>
{
    public async Task Handle(UpdateQualityHealthWeightsCommand command, CancellationToken ct)
    {
        var parsed = new Dictionary<QualityHealthCategory, int>();
        foreach (var weight in command.Weights)
        {
            if (!Enum.TryParse<QualityHealthCategory>(weight.Category, ignoreCase: true, out var category))
            {
                throw new DomainException(
                    "QHP-005", $"'{weight.Category}' is not a quality health category.");
            }

            parsed[category] = weight.Weight;
        }

        var profile = await db.QualityHealthProfiles
            .Include(p => p.Weights)
            .FirstOrDefaultAsync(ct);

        if (profile is null)
        {
            profile = QualityHealthProfile.CreateDefault();
            db.QualityHealthProfiles.Add(profile);
        }

        profile.ReplaceWeights(parsed, command.Reason);
        await db.SaveChangesAsync(ct);
    }
}
