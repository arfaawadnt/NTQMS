using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Platform;
using NT.QAMS.Domain.Organization;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Organization;

// â”€â”€ Interested parties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record RegisterInterestedPartyCommand(
    string Name, string Category, string NeedsAndExpectations,
    string? RelevantRequirements, DateOnly ReviewedOn) : ICommand<Guid>;
[RequireInternalActor]
public sealed record ReviseInterestedPartyCommand(
    Guid PartyId, string Name, string Category, string NeedsAndExpectations,
    string? RelevantRequirements, DateOnly ReviewedOn) : ICommand;
[RequireInternalActor]
public sealed record ArchiveInterestedPartyCommand(Guid PartyId) : ICommand;

public sealed class RegisterInterestedPartyValidator : AbstractValidator<RegisterInterestedPartyCommand>
{
    public RegisterInterestedPartyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NeedsAndExpectations).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.RelevantRequirements).MaximumLength(4000);
    }
}

public sealed class InterestedPartyHandlers(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs) :
    ICommandHandler<RegisterInterestedPartyCommand, Guid>,
    ICommandHandler<ReviseInterestedPartyCommand>,
    ICommandHandler<ArchiveInterestedPartyCommand>
{
    public async Task<Guid> Handle(RegisterInterestedPartyCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var partyRef = await refs.NextAsync(tenantId, "IP", ct);
        var party = InterestedParty.Register(
            partyRef, c.Name, c.Category, c.NeedsAndExpectations, c.RelevantRequirements, c.ReviewedOn);
        db.InterestedParties.Add(party);
        await db.SaveChangesAsync(ct);
        return party.Id;
    }

    public async Task Handle(ReviseInterestedPartyCommand c, CancellationToken ct)
    {
        var party = await LoadAsync(c.PartyId, ct);
        party.Revise(c.Name, c.Category, c.NeedsAndExpectations, c.RelevantRequirements, c.ReviewedOn);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ArchiveInterestedPartyCommand c, CancellationToken ct)
    {
        var party = await LoadAsync(c.PartyId, ct);
        party.Archive();
        await db.SaveChangesAsync(ct);
    }

    private async Task<InterestedParty> LoadAsync(Guid id, CancellationToken ct) =>
        await db.InterestedParties.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("IP-404", "Interested party not found.");
}

public sealed record GetInterestedPartiesQuery : IQuery<IReadOnlyList<InterestedPartyDto>>;

public sealed class GetInterestedPartiesHandler(IAppDbContext db)
    : IQueryHandler<GetInterestedPartiesQuery, IReadOnlyList<InterestedPartyDto>>
{
    public async Task<IReadOnlyList<InterestedPartyDto>> Handle(GetInterestedPartiesQuery q, CancellationToken ct) =>
        await db.InterestedParties.AsNoTracking()
            .OrderBy(p => p.Status).ThenBy(p => p.Name)
            .Select(p => new InterestedPartyDto(
                p.Id, p.PartyRef, p.Name, p.Category, p.NeedsAndExpectations,
                p.RelevantRequirements, p.ReviewedOn, p.Status.ToString()))
            .ToListAsync(ct);
}

// â”€â”€ Context issues â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record RegisterContextIssueCommand(
    string Type, string Category, string Description, string Impact) : ICommand<Guid>;
[RequireInternalActor]
public sealed record ReviseContextIssueCommand(
    Guid IssueId, string Type, string Category, string Description, string Impact) : ICommand;
[RequireInternalActor]
public sealed record LinkContextIssueRiskCommand(Guid IssueId, Guid RiskId) : ICommand;
[RequireInternalActor]
public sealed record CloseContextIssueCommand(Guid IssueId, string Resolution) : ICommand;

// The former varchar bound, kept at the API layer now the column is text (schema hardening 1.2/Q6).
public sealed class CloseContextIssueValidator : AbstractValidator<CloseContextIssueCommand>
{
    public CloseContextIssueValidator()
    {
        RuleFor(x => x.Resolution).NotEmpty().MaximumLength(4000);
    }
}

public sealed class RegisterContextIssueValidator : AbstractValidator<RegisterContextIssueCommand>
{
    public RegisterContextIssueValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Impact).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ContextIssueHandlers(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs) :
    ICommandHandler<RegisterContextIssueCommand, Guid>,
    ICommandHandler<ReviseContextIssueCommand>,
    ICommandHandler<LinkContextIssueRiskCommand>,
    ICommandHandler<CloseContextIssueCommand>
{
    public async Task<Guid> Handle(RegisterContextIssueCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var issueRef = await refs.NextAsync(tenantId, "CTX", ct);
        var issue = ContextIssue.Register(
            issueRef, Enum.Parse<ContextIssueType>(c.Type, ignoreCase: true),
            c.Category, c.Description, c.Impact);
        db.ContextIssues.Add(issue);
        await db.SaveChangesAsync(ct);
        return issue.Id;
    }

    public async Task Handle(ReviseContextIssueCommand c, CancellationToken ct)
    {
        var issue = await LoadAsync(c.IssueId, ct);
        issue.Revise(Enum.Parse<ContextIssueType>(c.Type, ignoreCase: true), c.Category, c.Description, c.Impact);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(LinkContextIssueRiskCommand c, CancellationToken ct)
    {
        // The risk must exist in this tenant's register â€” a dangling link is worse than none.
        if (!await db.Risks.AnyAsync(r => r.Id == c.RiskId, ct))
        {
            throw new DomainException("RSK-404", "Risk not found.");
        }

        var issue = await LoadAsync(c.IssueId, ct);
        issue.LinkRisk(c.RiskId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CloseContextIssueCommand c, CancellationToken ct)
    {
        var issue = await LoadAsync(c.IssueId, ct);
        issue.Close(c.Resolution);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ContextIssue> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ContextIssues.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new DomainException("CTX-404", "Context issue not found.");
}

public sealed record GetContextIssuesQuery : IQuery<IReadOnlyList<ContextIssueDto>>;

public sealed class GetContextIssuesHandler(IAppDbContext db)
    : IQueryHandler<GetContextIssuesQuery, IReadOnlyList<ContextIssueDto>>
{
    public async Task<IReadOnlyList<ContextIssueDto>> Handle(GetContextIssuesQuery q, CancellationToken ct) =>
        await db.ContextIssues.AsNoTracking()
            .OrderBy(i => i.Status).ThenBy(i => i.IssueRef)
            .Select(i => new ContextIssueDto(
                i.Id, i.IssueRef, i.Type.ToString(), i.Category, i.Description, i.Impact,
                i.LinkedRiskId, i.Status.ToString(), i.Resolution))
            .ToListAsync(ct);
}
