using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Accreditation;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Accreditation.Commands;

// ── Standard set definition ──────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Standards, PermissionAction.Create)]
public sealed record DefineStandardSetCommand(AccreditationFramework Framework, string Name, string Version)
    : ICommand<Guid>;

public sealed class DefineStandardSetValidator : AbstractValidator<DefineStandardSetCommand>
{
    public DefineStandardSetValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(40);
    }
}

public sealed class DefineStandardSetHandler(IAppDbContext db) : ICommandHandler<DefineStandardSetCommand, Guid>
{
    public async Task<Guid> Handle(DefineStandardSetCommand c, CancellationToken ct)
    {
        var set = StandardSet.Define(c.Framework, c.Name, c.Version);
        db.StandardSets.Add(set);
        await db.SaveChangesAsync(ct);
        return set.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Standards, PermissionAction.Create)]
public sealed record AddStandardElementCommand(
    Guid StandardSetId, string ChapterCode, string ChapterTitle, string StandardCode,
    string ElementCode, string Text, int Weight) : ICommand<Guid>;

public sealed class AddStandardElementValidator : AbstractValidator<AddStandardElementCommand>
{
    public AddStandardElementValidator()
    {
        RuleFor(x => x.ChapterCode).MaximumLength(40);
        RuleFor(x => x.ChapterTitle).MaximumLength(300);
        RuleFor(x => x.StandardCode).MaximumLength(40);
        RuleFor(x => x.ElementCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Weight).InclusiveBetween(1, 10);
    }
}

public sealed class AddStandardElementHandler(IAppDbContext db) : ICommandHandler<AddStandardElementCommand, Guid>
{
    public async Task<Guid> Handle(AddStandardElementCommand c, CancellationToken ct)
    {
        var set = await Load(db, c.StandardSetId, ct);
        var id = set.AddElement(c.ChapterCode, c.ChapterTitle, c.StandardCode, c.ElementCode, c.Text, c.Weight);
        await db.SaveChangesAsync(ct);
        return id;
    }

    internal static async Task<StandardSet> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.StandardSets.Include(s => s.Elements).SingleOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new DomainException("STD-404", "Standard set not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Standards, PermissionAction.Approve)]
public sealed record ActivateStandardSetCommand(Guid StandardSetId) : ICommand;

public sealed class ActivateStandardSetHandler(IAppDbContext db) : ICommandHandler<ActivateStandardSetCommand>
{
    public async Task Handle(ActivateStandardSetCommand c, CancellationToken ct)
    {
        (await AddStandardElementHandler.Load(db, c.StandardSetId, ct)).Activate();
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Standards, PermissionAction.Void)]
public sealed record ArchiveStandardSetCommand(Guid StandardSetId) : ICommand;

public sealed class ArchiveStandardSetHandler(IAppDbContext db) : ICommandHandler<ArchiveStandardSetCommand>
{
    public async Task Handle(ArchiveStandardSetCommand c, CancellationToken ct)
    {
        (await AddStandardElementHandler.Load(db, c.StandardSetId, ct)).Archive();
        await db.SaveChangesAsync(ct);
    }
}

// ── Self-assessment ──────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Standards, PermissionAction.Edit)]
public sealed record AssessElementCommand(
    Guid StandardSetId, Guid ElementId, ComplianceStatus Status, string? Note) : ICommand;

public sealed class AssessElementValidator : AbstractValidator<AssessElementCommand>
{
    public AssessElementValidator() => RuleFor(x => x.Note).MaximumLength(2000);
}

public sealed class AssessElementHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<AssessElementCommand>
{
    public async Task Handle(AssessElementCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var set = await AddStandardElementHandler.Load(db, c.StandardSetId, ct);
        set.AssessElement(c.ElementId, c.Status, c.Note, actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

// ── Evidence linking ─────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Standards, PermissionAction.Edit)]
public sealed record LinkEvidenceCommand(
    Guid StandardSetId, Guid ElementId, EvidenceSourceType SourceType, Guid SourceId,
    string SourceRef, string? Description) : ICommand<Guid>;

public sealed class LinkEvidenceValidator : AbstractValidator<LinkEvidenceCommand>
{
    public LinkEvidenceValidator()
    {
        RuleFor(x => x.SourceRef).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class LinkEvidenceHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<LinkEvidenceCommand, Guid>
{
    public async Task<Guid> Handle(LinkEvidenceCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        // The element must belong to the named set (tenant filter already applies).
        var set = await AddStandardElementHandler.Load(db, c.StandardSetId, ct);
        if (set.Elements.All(e => e.Id != c.ElementId))
        {
            throw new DomainException("STD-019", "Element not found in this set.");
        }

        var link = EvidenceLink.Create(
            c.StandardSetId, c.ElementId, c.SourceType, c.SourceId, c.SourceRef, c.Description, actor, clock.UtcNow);
        db.EvidenceLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return link.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Standards, PermissionAction.Edit)]
public sealed record UnlinkEvidenceCommand(Guid EvidenceId) : ICommand;

public sealed class UnlinkEvidenceHandler(IAppDbContext db) : ICommandHandler<UnlinkEvidenceCommand>
{
    public async Task Handle(UnlinkEvidenceCommand c, CancellationToken ct)
    {
        var link = await db.EvidenceLinks.SingleOrDefaultAsync(e => e.Id == c.EvidenceId, ct)
            ?? throw new DomainException("EVD-404", "Evidence link not found.");
        db.EvidenceLinks.Remove(link);
        await db.SaveChangesAsync(ct);
    }
}
