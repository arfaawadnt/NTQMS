using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Committees;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Committees;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Committees;

// ── Commands ─────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Create)]
public sealed record CreateCommitteeCommand(
    string Name, string TermsOfReference, CommitteeFrequency Frequency, int QuorumSize) : ICommand<Guid>;

public sealed class CreateCommitteeValidator : AbstractValidator<CreateCommitteeCommand>
{
    public CreateCommitteeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TermsOfReference).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.QuorumSize).GreaterThanOrEqualTo(1);
    }
}

public sealed class CreateCommitteeHandler(IAppDbContext db) : ICommandHandler<CreateCommitteeCommand, Guid>
{
    public async Task<Guid> Handle(CreateCommitteeCommand c, CancellationToken ct)
    {
        var committee = Committee.Create(c.Name, c.TermsOfReference, c.Frequency, c.QuorumSize);
        db.Committees.Add(committee);
        await db.SaveChangesAsync(ct);
        return committee.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record AddCommitteeMemberCommand(Guid CommitteeId, Guid UserId, string RoleTitle) : ICommand<Guid>;

public sealed class AddCommitteeMemberValidator : AbstractValidator<AddCommitteeMemberCommand>
{
    public AddCommitteeMemberValidator() => RuleFor(x => x.RoleTitle).NotEmpty().MaximumLength(100);
}

public sealed class AddCommitteeMemberHandler(IAppDbContext db) : ICommandHandler<AddCommitteeMemberCommand, Guid>
{
    public async Task<Guid> Handle(AddCommitteeMemberCommand c, CancellationToken ct)
    {
        var committee = await Load(db, c.CommitteeId, ct);
        var id = committee.AddMember(c.UserId, c.RoleTitle);
        await db.SaveChangesAsync(ct);
        return id;
    }

    internal static async Task<Committee> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Committees.Include(x => x.Members).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("CMT-404", "Committee not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record RemoveCommitteeMemberCommand(Guid CommitteeId, Guid MemberId) : ICommand;

public sealed class RemoveCommitteeMemberHandler(IAppDbContext db) : ICommandHandler<RemoveCommitteeMemberCommand>
{
    public async Task Handle(RemoveCommitteeMemberCommand c, CancellationToken ct)
    {
        var committee = await AddCommitteeMemberHandler.Load(db, c.CommitteeId, ct);
        committee.RemoveMember(c.MemberId);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Edit)]
public sealed record UpdateQuorumCommand(Guid CommitteeId, int QuorumSize) : ICommand;

public sealed class UpdateQuorumHandler(IAppDbContext db) : ICommandHandler<UpdateQuorumCommand>
{
    public async Task Handle(UpdateQuorumCommand c, CancellationToken ct)
    {
        var committee = await AddCommitteeMemberHandler.Load(db, c.CommitteeId, ct);
        committee.UpdateQuorum(c.QuorumSize);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Committees, PermissionAction.Void)]
public sealed record DisbandCommitteeCommand(Guid CommitteeId) : ICommand;

public sealed class DisbandCommitteeHandler(IAppDbContext db) : ICommandHandler<DisbandCommitteeCommand>
{
    public async Task Handle(DisbandCommitteeCommand c, CancellationToken ct)
    {
        (await AddCommitteeMemberHandler.Load(db, c.CommitteeId, ct)).Disband();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetCommitteesQuery(string? Status = null) : IQuery<IReadOnlyList<CommitteeListItemDto>>;

public sealed class GetCommitteesHandler(IAppDbContext db)
    : IQueryHandler<GetCommitteesQuery, IReadOnlyList<CommitteeListItemDto>>
{
    public async Task<IReadOnlyList<CommitteeListItemDto>> Handle(GetCommitteesQuery q, CancellationToken ct)
    {
        var query = db.Committees.AsNoTracking().Include(c => c.Members).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(c => c.Status.ToString() == q.Status);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CommitteeListItemDto(
                c.Id, c.Name, c.Frequency.ToString(), c.QuorumSize, c.Status.ToString(), c.Members.Count))
            .ToListAsync(ct);
    }
}

public sealed record GetCommitteeByIdQuery(Guid CommitteeId) : IQuery<CommitteeDetailDto>;

public sealed class GetCommitteeByIdHandler(IAppDbContext db) : IQueryHandler<GetCommitteeByIdQuery, CommitteeDetailDto>
{
    public async Task<CommitteeDetailDto> Handle(GetCommitteeByIdQuery q, CancellationToken ct)
    {
        var c = await db.Committees.AsNoTracking().Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == q.CommitteeId, ct)
            ?? throw new DomainException("CMT-404", "Committee not found.");

        return new CommitteeDetailDto(
            c.Id, c.Name, c.TermsOfReference, c.Frequency.ToString(), c.QuorumSize, c.Status.ToString(),
            c.Members.Select(m => new CommitteeMemberDto(m.Id, m.UserId, m.RoleTitle)).ToList());
    }
}
