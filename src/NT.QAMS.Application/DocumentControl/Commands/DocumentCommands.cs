using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.DocumentControl.Commands;

[RequireInternalActor]
public sealed record CreateDocumentCommand(
    string Code, string Title, string Category, Guid FileId, string ChangeSummary,
    int ReviewCycleMonths = 24) : ICommand<Guid>;

public sealed class CreateDocumentValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40)
            .Matches("^[A-Za-z0-9][A-Za-z0-9-]*$")
            .WithMessage("Code must be letters, digits and hyphens (e.g. SOP-CAL-045).");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Category).MaximumLength(50);
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.ChangeSummary).MaximumLength(1000);
    }
}

public sealed class CreateDocumentHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<CreateDocumentCommand, Guid>
{
    public async Task<Guid> Handle(CreateDocumentCommand command, CancellationToken ct)
    {
        var author = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var code = command.Code.Trim().ToUpperInvariant();
        if (await db.Documents.AnyAsync(d => d.Code == code, ct))
        {
            throw new DomainException("DOC-003", $"Document code '{code}' is already in use.");
        }

        // The file must exist and belong to this tenant (global filter enforces the latter).
        if (!await db.Files.AnyAsync(f => f.Id == command.FileId, ct))
        {
            throw new DomainException("FILE-404", "Uploaded file not found.");
        }

        var doc = ControlledDocument.Create(
            code, command.Title, command.Category, command.FileId, command.ChangeSummary, author,
            command.ReviewCycleMonths);

        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc.Id;
    }
}

[RequireInternalActor]
public sealed record SubmitDocumentForReviewCommand(Guid DocumentId) : ICommand;
[RequireInternalActor]
public sealed record RecommendDocumentCommand(Guid DocumentId) : ICommand;
[RequireInternalActor]
public sealed record RejectDocumentVersionCommand(Guid DocumentId, string Reason) : ICommand;
/// <summary>Publishing is a Part 11 signing ceremony: it requires the approver's e-signature PIN.</summary>
[RequirePermissionPolicy(NT.QAMS.Domain.Authorization.PermissionCatalog.Documents,
    NT.QAMS.Domain.Authorization.PermissionAction.Sign)]
public sealed record PublishDocumentCommand(Guid DocumentId, string Password, string Pin) : ICommand;
[RequireInternalActor]
public sealed record DraftNewVersionCommand(
    Guid DocumentId, Guid FileId, string ChangeSummary, VersionBump Bump) : ICommand;
[RequireInternalActor]
public sealed record RetireDocumentCommand(Guid DocumentId) : ICommand;

public sealed class RejectDocumentVersionValidator : AbstractValidator<RejectDocumentVersionCommand>
{
    public RejectDocumentVersionValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

internal static class DocumentLoader
{
    public static async Task<ControlledDocument> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Documents.Include(d => d.Versions).SingleOrDefaultAsync(d => d.Id == id, ct)
        ?? throw new DomainException("DOC-404", "Document not found.");

    public static Guid RequireActor(ICurrentUser user) =>
        user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
}

public sealed class SubmitDocumentForReviewHandler(IAppDbContext db)
    : ICommandHandler<SubmitDocumentForReviewCommand>
{
    public async Task Handle(SubmitDocumentForReviewCommand c, CancellationToken ct)
    {
        (await DocumentLoader.LoadAsync(db, c.DocumentId, ct)).SubmitForReview();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RecommendDocumentHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<RecommendDocumentCommand>
{
    public async Task Handle(RecommendDocumentCommand c, CancellationToken ct)
    {
        (await DocumentLoader.LoadAsync(db, c.DocumentId, ct))
            .Recommend(DocumentLoader.RequireActor(user), clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RejectDocumentVersionHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<RejectDocumentVersionCommand>
{
    public async Task Handle(RejectDocumentVersionCommand c, CancellationToken ct)
    {
        (await DocumentLoader.LoadAsync(db, c.DocumentId, ct))
            .RejectVersion(DocumentLoader.RequireActor(user), c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class PublishDocumentHandler(
    IAppDbContext db, ICurrentUser user, IClock clock, IESignatureService signatures)
    : ICommandHandler<PublishDocumentCommand>
{
    public async Task Handle(PublishDocumentCommand c, CancellationToken ct)
    {
        var actor = DocumentLoader.RequireActor(user);
        var doc = await DocumentLoader.LoadAsync(db, c.DocumentId, ct);

        var approving = doc.InFlightVersion
            ?? throw new SharedKernel.Primitives.DomainException("DOC-014", "No version is awaiting approval.");

        // Pre-validate every publish precondition BEFORE minting the signature â€”
        // the signature ledger is append-only, so a signature must never exist
        // for a publish that then fails its state or SoD gates.
        if (approving.State != Domain.DocumentControl.VersionState.Approved)
        {
            throw new SharedKernel.Primitives.InvalidStateTransitionException(
                "DOC-014", $"Cannot publish a version in state {approving.State}.");
        }

        if (actor == approving.AuthorId)
        {
            throw new SharedKernel.Primitives.DomainException(
                "SOD-DOC-002", "Segregation of duties: the author cannot approve their own document.");
        }

        // Content hash of the exact bytes being approved (Part 11 signatureâ†”record link).
        var contentHash = await db.Files
            .Where(f => f.Id == approving.FileId).Select(f => f.Sha256).SingleAsync(ct);

        // Verify both signature components and mint the immutable signature,
        // then apply the (already pre-validated) state change.
        await signatures.SignAsync(
            actor, c.Password, c.Pin,
            $"Approved and published {doc.Code} v{approving.VersionLabel}",
            $"DOC:{doc.Id:N}", contentHash, ct);

        doc.Publish(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DraftNewVersionHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<DraftNewVersionCommand>
{
    public async Task Handle(DraftNewVersionCommand c, CancellationToken ct)
    {
        if (!await db.Files.AnyAsync(f => f.Id == c.FileId, ct))
        {
            throw new DomainException("FILE-404", "Uploaded file not found.");
        }

        (await DocumentLoader.LoadAsync(db, c.DocumentId, ct))
            .DraftNewVersion(c.FileId, c.ChangeSummary, c.Bump, DocumentLoader.RequireActor(user));
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RetireDocumentHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<RetireDocumentCommand>
{
    public async Task Handle(RetireDocumentCommand c, CancellationToken ct)
    {
        (await DocumentLoader.LoadAsync(db, c.DocumentId, ct))
            .Retire(DocumentLoader.RequireActor(user));
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Periodic review (ISO 17025 Â§8.3) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record ConfirmDocumentReviewCommand(Guid DocumentId) : ICommand;

public sealed class ConfirmDocumentReviewHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<ConfirmDocumentReviewCommand>
{
    public async Task Handle(ConfirmDocumentReviewCommand c, CancellationToken ct)
    {
        var actor = DocumentLoader.RequireActor(user);
        var doc = await DocumentLoader.LoadAsync(db, c.DocumentId, ct);
        doc.ConfirmPeriodicReview(actor, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
    }
}
