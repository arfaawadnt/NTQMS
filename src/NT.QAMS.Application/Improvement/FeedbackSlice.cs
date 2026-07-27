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
public sealed record LogFeedbackCommand(
    string Source, string Channel, string Type, string Subject, string Details,
    int? SatisfactionScore, DateOnly ReceivedOn,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class LogFeedbackValidator : AbstractValidator<LogFeedbackCommand>
{
    public LogFeedbackValidator()
    {
        RuleFor(x => x.Source).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Channel).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Details).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.SatisfactionScore).InclusiveBetween(1, 5).When(x => x.SatisfactionScore is not null);
    }
}

public sealed class LogFeedbackHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs)
    : ICommandHandler<LogFeedbackCommand, Guid>
{
    public async Task<Guid> Handle(LogFeedbackCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var feedbackRef = await refs.NextAsync(tenantId, "FB", ct);
        var feedback = FeedbackEntry.Log(
            feedbackRef, c.Source, c.Channel, Enum.Parse<FeedbackType>(c.Type, ignoreCase: true),
            c.Subject, c.Details, c.SatisfactionScore, c.ReceivedOn, actor);
        feedback.BranchId = c.BranchId;
        feedback.DepartmentId = c.DepartmentId;
        db.FeedbackEntries.Add(feedback);
        await db.SaveChangesAsync(ct);
        return feedback.Id;
    }
}

[RequireInternalActor]
public sealed record ReviewFeedbackCommand(Guid FeedbackId, string ReviewNotes) : ICommand;
[RequireInternalActor]
public sealed record CloseFeedbackCommand(Guid FeedbackId, string ActionSummary) : ICommand;
[RequireInternalActor]
public sealed record EscalateFeedbackCommand(Guid FeedbackId, string ComplainantName, string? ComplainantContact) : ICommand<Guid>;

public sealed class ReviewFeedbackValidator : AbstractValidator<ReviewFeedbackCommand>
{
    public ReviewFeedbackValidator()
    {
        RuleFor(x => x.ReviewNotes).NotEmpty().MaximumLength(2000);
    }
}

public sealed class CloseFeedbackValidator : AbstractValidator<CloseFeedbackCommand>
{
    public CloseFeedbackValidator()
    {
        RuleFor(x => x.ActionSummary).NotEmpty().MaximumLength(2000);
    }
}

public sealed class FeedbackWorkflowHandlers(
    IAppDbContext db, ICurrentUser user, IReferenceNumberGenerator refs, IClock clock) :
    ICommandHandler<ReviewFeedbackCommand>,
    ICommandHandler<CloseFeedbackCommand>,
    ICommandHandler<EscalateFeedbackCommand, Guid>
{
    public async Task Handle(ReviewFeedbackCommand c, CancellationToken ct)
    {
        var feedback = await LoadAsync(c.FeedbackId, ct);
        feedback.Review(c.ReviewNotes);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CloseFeedbackCommand c, CancellationToken ct)
    {
        var feedback = await LoadAsync(c.FeedbackId, ct);
        feedback.Close(c.ActionSummary);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Escalation opens a formal complaint carrying the feedback content and
    /// links the two records in the SAME transaction â€” no half-escalated state.
    /// </summary>
    public async Task<Guid> Handle(EscalateFeedbackCommand c, CancellationToken ct)
    {
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var feedback = await LoadAsync(c.FeedbackId, ct);

        var complaintRef = await refs.NextAsync(feedback.TenantId, "CMP", ct);
        var complaint = Complaint.Log(
            complaintRef, ComplaintChannel.Portal, c.ComplainantName, c.ComplainantContact,
            confidential: false,
            subject: $"Escalated feedback {feedback.FeedbackRef}: {feedback.Subject}",
            description: feedback.Details,
            loggedBy: actor, at: clock.UtcNow);
        complaint.TenantId = feedback.TenantId;
        complaint.BranchId = feedback.BranchId;
        complaint.DepartmentId = feedback.DepartmentId;

        db.Complaints.Add(complaint);
        feedback.Escalate(complaint.Id);
        await db.SaveChangesAsync(ct);
        return complaint.Id;
    }

    private async Task<FeedbackEntry> LoadAsync(Guid id, CancellationToken ct) =>
        await db.FeedbackEntries.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new DomainException("FBK-404", "Feedback entry not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetFeedbackQuery(string? Status, string? Type)
    : IQuery<IReadOnlyList<FeedbackListItemDto>>;

public sealed class GetFeedbackHandler(IAppDbContext db)
    : IQueryHandler<GetFeedbackQuery, IReadOnlyList<FeedbackListItemDto>>
{
    public async Task<IReadOnlyList<FeedbackListItemDto>> Handle(GetFeedbackQuery q, CancellationToken ct)
    {
        var entries = db.FeedbackEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<FeedbackStatus>(q.Status, ignoreCase: true, out var status))
        {
            entries = entries.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q.Type)
            && Enum.TryParse<FeedbackType>(q.Type, ignoreCase: true, out var type))
        {
            entries = entries.Where(f => f.Type == type);
        }

        return await entries
            .OrderByDescending(f => f.ReceivedOn)
            .Select(f => new FeedbackListItemDto(
                f.Id, f.FeedbackRef, f.Source, f.Channel, f.Type.ToString(), f.Subject,
                f.SatisfactionScore, f.ReceivedOn, f.Status.ToString(),
                f.BranchId, f.DepartmentId))
            .ToListAsync(ct);
    }
}

public sealed record GetFeedbackByIdQuery(Guid FeedbackId) : IQuery<FeedbackDetailDto>;

public sealed class GetFeedbackByIdHandler(IAppDbContext db)
    : IQueryHandler<GetFeedbackByIdQuery, FeedbackDetailDto>
{
    public async Task<FeedbackDetailDto> Handle(GetFeedbackByIdQuery q, CancellationToken ct)
    {
        var f = await db.FeedbackEntries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.FeedbackId, ct)
            ?? throw new DomainException("FBK-404", "Feedback entry not found.");

        return new FeedbackDetailDto(
            f.Id, f.FeedbackRef, f.Source, f.Channel, f.Type.ToString(), f.Subject, f.Details,
            f.SatisfactionScore, f.ReceivedOn, f.LoggedBy, f.Status.ToString(),
            f.ReviewNotes, f.ActionSummary, f.ComplaintId, f.BranchId, f.DepartmentId);
    }
}
