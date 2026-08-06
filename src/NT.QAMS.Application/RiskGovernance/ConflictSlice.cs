using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Compliance;
using NT.QAMS.Contracts.Governance;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.RiskGovernance;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record DeclareConflictCommand(
    Guid DeclarantId, string Description, string RelatedParty, DateOnly DeclaredOn) : ICommand<Guid>;
/// <summary>Assessing a conflict of interest is a Part 11 signing ceremony: it requires the assessor's e-signature (account password + PIN).</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Conflicts,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record AssessConflictCommand(Guid ConflictId, string RiskLevel, string Mitigation, string Password, string Pin) : ICommand;
[RequireInternalActor]
public sealed record CloseConflictCommand(Guid ConflictId, string Outcome, string ClosureNote) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class CloseConflictValidator : AbstractValidator<CloseConflictCommand>
{
    public CloseConflictValidator()
    {
        RuleFor(x => x.ClosureNote).NotEmpty().MaximumLength(2000);
    }
}

public sealed class DeclareConflictValidator : AbstractValidator<DeclareConflictCommand>
{
    public DeclareConflictValidator()
    {
        RuleFor(x => x.DeclarantId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.RelatedParty).NotEmpty().MaximumLength(300);
    }
}

public sealed class AssessConflictValidator : AbstractValidator<AssessConflictCommand>
{
    public AssessConflictValidator()
    {
        RuleFor(x => x.RiskLevel).NotEmpty();
        RuleFor(x => x.Mitigation).NotEmpty().MaximumLength(2000);
    }
}

public sealed class DeclareConflictHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<DeclareConflictCommand, Guid>
{
    public async Task<Guid> Handle(DeclareConflictCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var conflictRef = await refs.NextAsync(tenantId, "COI", ct);
        var conflict = ConflictDeclaration.Declare(
            conflictRef, c.DeclarantId, c.Description, c.RelatedParty, c.DeclaredOn);
        db.ConflictDeclarations.Add(conflict);
        await db.SaveChangesAsync(ct);
        return conflict.Id;
    }
}

public sealed class ConflictWorkflowHandlers(IAppDbContext db, ICurrentUser user, IESignatureService signatures) :
    ICommandHandler<AssessConflictCommand>,
    ICommandHandler<CloseConflictCommand>
{
    public async Task Handle(AssessConflictCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var conflict = await LoadAsync(c.ConflictId, ct);

        // Pre-validate before minting (append-only ledger; mirrors the pilot). The aggregate re-checks both.
        if (conflict.Status != ConflictStatus.Declared)
        {
            throw new InvalidStateTransitionException(
                "COI-010", $"Only a declared conflict can be assessed (current: {conflict.Status}).");
        }

        if (actor == conflict.DeclarantId)
        {
            throw new DomainException(
                "SOD-COI-001", "Segregation of duties: declarants cannot assess their own conflict.");
        }

        var subjectRef = $"COI:{conflict.Id:N}";
        await signatures.SignAsync(
            actor, c.Password, c.Pin, $"Assessed conflict of interest {conflict.ConflictRef}", subjectRef,
            SignatureContentHash.Compute(("conflict", conflict.ConflictRef), ("outcome", "assessed")), ct);

        conflict.Assess(actor, Enum.Parse<ConflictRiskLevel>(c.RiskLevel, ignoreCase: true), c.Mitigation);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CloseConflictCommand c, CancellationToken ct)
    {
        var conflict = await LoadAsync(c.ConflictId, ct);
        conflict.Close(Enum.Parse<ConflictOutcome>(c.Outcome, ignoreCase: true), c.ClosureNote);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ConflictDeclaration> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ConflictDeclarations.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("COI-404", "Conflict declaration not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetConflictsQuery(string? Status) : IQuery<IReadOnlyList<ConflictListItemDto>>;

public sealed class GetConflictsHandler(IAppDbContext db)
    : IQueryHandler<GetConflictsQuery, IReadOnlyList<ConflictListItemDto>>
{
    public async Task<IReadOnlyList<ConflictListItemDto>> Handle(GetConflictsQuery q, CancellationToken ct)
    {
        var conflicts = db.ConflictDeclarations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<ConflictStatus>(q.Status, ignoreCase: true, out var status))
        {
            conflicts = conflicts.Where(x => x.Status == status);
        }

        return await conflicts
            .OrderByDescending(x => x.DeclaredOn)
            .Select(x => new ConflictListItemDto(
                x.Id, x.ConflictRef, x.DeclarantId, x.RelatedParty, x.DeclaredOn,
                x.Status.ToString(), x.RiskLevel != null ? x.RiskLevel.ToString() : null,
                x.Outcome != null ? x.Outcome.ToString() : null))
            .ToListAsync(ct);
    }
}

public sealed record GetConflictByIdQuery(Guid ConflictId) : IQuery<ConflictDetailDto>;

public sealed class GetConflictByIdHandler(IAppDbContext db)
    : IQueryHandler<GetConflictByIdQuery, ConflictDetailDto>
{
    public async Task<ConflictDetailDto> Handle(GetConflictByIdQuery q, CancellationToken ct)
    {
        var x = await db.ConflictDeclarations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == q.ConflictId, ct)
            ?? throw new DomainException("COI-404", "Conflict declaration not found.");

        return new ConflictDetailDto(
            x.Id, x.ConflictRef, x.DeclarantId, x.Description, x.RelatedParty, x.DeclaredOn,
            x.Status.ToString(), x.RiskLevel?.ToString(), x.Mitigation, x.AssessedBy,
            x.Outcome?.ToString(), x.ClosureNote);
    }
}
