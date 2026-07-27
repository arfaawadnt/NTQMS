using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.DocumentControl;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.DocumentControl;

// â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/// <summary>Issue a numbered physical copy of a published document to a named holder.</summary>
[RequireInternalActor]
public sealed record IssueControlledCopyCommand(Guid DocumentId, string Holder) : ICommand<Guid>;
[RequireInternalActor]
public sealed record CloseControlledCopyCommand(Guid CopyId, string Outcome) : ICommand;

public sealed class IssueControlledCopyHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<IssueControlledCopyCommand, Guid>
{
    public async Task<Guid> Handle(IssueControlledCopyCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var document = await db.Documents.Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == c.DocumentId, ct)
            ?? throw new DomainException("DOC-404", "Document not found.");

        var published = document.PublishedVersion
            ?? throw new DomainException("CCP-020", "Only a published document can have a controlled copy issued.");

        var lastCopyNumber = await db.DocumentControlledCopies
            .Where(x => x.DocumentId == document.Id)
            .Select(x => (int?)x.CopyNumber)
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync(ct) ?? 0;

        var copy = DocumentControlledCopy.Issue(
            document.Id, document.Code, published.VersionLabel, lastCopyNumber + 1, c.Holder, actor, clock.UtcNow);
        db.DocumentControlledCopies.Add(copy);
        await db.SaveChangesAsync(ct);
        return copy.Id;
    }
}

public sealed class CloseControlledCopyHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<CloseControlledCopyCommand>
{
    public async Task Handle(CloseControlledCopyCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        if (!Enum.TryParse<ControlledCopyStatus>(c.Outcome, ignoreCase: true, out var outcome))
        {
            throw new DomainException("CCP-003", "The outcome must be Returned or Destroyed.");
        }

        var copy = await db.DocumentControlledCopies.FirstOrDefaultAsync(x => x.Id == c.CopyId, ct)
            ?? throw new DomainException("CCP-404", "Controlled copy not found.");
        copy.Close(outcome, actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Query â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetControlledCopiesQuery(Guid DocumentId) : IQuery<IReadOnlyList<ControlledCopyDto>>;

public sealed class GetControlledCopiesHandler(IAppDbContext db)
    : IQueryHandler<GetControlledCopiesQuery, IReadOnlyList<ControlledCopyDto>>
{
    public async Task<IReadOnlyList<ControlledCopyDto>> Handle(GetControlledCopiesQuery q, CancellationToken ct) =>
        await db.DocumentControlledCopies.AsNoTracking()
            .Where(c => c.DocumentId == q.DocumentId)
            .OrderBy(c => c.CopyNumber)
            .Select(c => new ControlledCopyDto(
                c.Id, c.CopyNumber, c.VersionLabel, c.Holder, c.Status.ToString(),
                c.IssuedBy, c.IssuedAtUtc, c.ClosedAtUtc))
            .ToListAsync(ct);
}
