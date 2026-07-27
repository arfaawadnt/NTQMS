using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Resources;
using NT.QAMS.Domain.Competency;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Competency;

// â”€â”€ Competency commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record AssignCompetencyCommand(
    Guid TraineeId, string Subject, Guid? DocumentId, int ValidityMonths) : ICommand<Guid>;

public sealed class AssignCompetencyValidator : AbstractValidator<AssignCompetencyCommand>
{
    public AssignCompetencyValidator()
    {
        RuleFor(x => x.TraineeId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ValidityMonths).InclusiveBetween(1, 60);
    }
}

public sealed class AssignCompetencyHandler(IAppDbContext db)
    : ICommandHandler<AssignCompetencyCommand, Guid>
{
    public async Task<Guid> Handle(AssignCompetencyCommand c, CancellationToken ct)
    {
        var record = CompetencyRecord.Assign(c.TraineeId, c.Subject, c.DocumentId, c.ValidityMonths);
        db.Competencies.Add(record);
        await db.SaveChangesAsync(ct);
        return record.Id;
    }
}

[RequireInternalActor]
public sealed record ScoreAssessmentCommand(Guid CompetencyId, int Score) : ICommand;
[RequireInternalActor]
public sealed record AuthorizeCompetencyCommand(Guid CompetencyId) : ICommand;
[RequireInternalActor]
public sealed record RevokeCompetencyCommand(Guid CompetencyId, string Reason) : ICommand;

internal static class CompetencyLoader
{
    public static async Task<CompetencyRecord> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Competencies
            .Include(x => x.Assessments)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("COMP-404", "Competency record not found.");

    public static Guid RequireActor(ICurrentUser user) =>
        user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
}

public sealed class ScoreAssessmentHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<ScoreAssessmentCommand>
{
    public async Task Handle(ScoreAssessmentCommand c, CancellationToken ct)
    {
        (await CompetencyLoader.LoadAsync(db, c.CompetencyId, ct))
            .ScoreAssessment(c.Score, CompetencyLoader.RequireActor(user), clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class AuthorizeCompetencyHandler(IAppDbContext db, ICurrentUser user, IClock clock)
    : ICommandHandler<AuthorizeCompetencyCommand>
{
    public async Task Handle(AuthorizeCompetencyCommand c, CancellationToken ct)
    {
        (await CompetencyLoader.LoadAsync(db, c.CompetencyId, ct))
            .Authorize(CompetencyLoader.RequireActor(user), DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RevokeCompetencyHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<RevokeCompetencyCommand>
{
    public async Task Handle(RevokeCompetencyCommand c, CancellationToken ct)
    {
        (await CompetencyLoader.LoadAsync(db, c.CompetencyId, ct))
            .Revoke(CompetencyLoader.RequireActor(user), c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Training commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record AssignTrainingCommand(
    Guid TraineeId, string Subject, Guid? DocumentId, DateOnly DueDate) : ICommand<Guid>;

public sealed class AssignTrainingHandler(IAppDbContext db)
    : ICommandHandler<AssignTrainingCommand, Guid>
{
    public async Task<Guid> Handle(AssignTrainingCommand c, CancellationToken ct)
    {
        var assignment = TrainingAssignment.Create(c.TraineeId, c.Subject, c.DocumentId, c.DueDate);
        db.TrainingAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return assignment.Id;
    }
}

[RequireInternalActor]
public sealed record CompleteTrainingCommand(Guid AssignmentId) : ICommand;

public sealed class CompleteTrainingHandler(IAppDbContext db, IClock clock)
    : ICommandHandler<CompleteTrainingCommand>
{
    public async Task Handle(CompleteTrainingCommand c, CancellationToken ct)
    {
        var assignment = await db.TrainingAssignments.SingleOrDefaultAsync(t => t.Id == c.AssignmentId, ct)
            ?? throw new DomainException("TRN-404", "Training assignment not found.");
        assignment.Complete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetCompetenciesQuery(
    Guid? TraineeId = null, string? Status = null,
    int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<CompetencyListItemDto>>;

public sealed class GetCompetenciesHandler(IAppDbContext db)
    : IQueryHandler<GetCompetenciesQuery, Contracts.Common.PagedResponse<CompetencyListItemDto>>
{
    public async Task<Contracts.Common.PagedResponse<CompetencyListItemDto>> Handle(
        GetCompetenciesQuery q, CancellationToken ct)
    {
        var query = db.Competencies.AsNoTracking();
        if (q.TraineeId is { } trainee)
        {
            query = query.Where(x => x.TraineeId == trainee);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(x => x.Status.ToString() == q.Status);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderBy(x => x.Subject)
            .Select(x => new CompetencyListItemDto(
                x.Id, x.TraineeId, x.Subject, x.Status.ToString(), x.ExpiresAt))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}

public sealed record GetCompetencyByIdQuery(Guid CompetencyId) : IQuery<CompetencyDetailDto>;

public sealed class GetCompetencyByIdHandler(IAppDbContext db)
    : IQueryHandler<GetCompetencyByIdQuery, CompetencyDetailDto>
{
    public async Task<CompetencyDetailDto> Handle(GetCompetencyByIdQuery q, CancellationToken ct)
    {
        var x = await db.Competencies
            .AsNoTracking()
            .Include(c => c.Assessments)
            .SingleOrDefaultAsync(c => c.Id == q.CompetencyId, ct)
            ?? throw new DomainException("COMP-404", "Competency record not found.");

        return new CompetencyDetailDto(
            x.Id, x.TraineeId, x.Subject, x.DocumentId, x.Status.ToString(),
            x.ValidityMonths, x.ExpiresAt, x.AuthorizedBy, x.RevocationReason,
            x.Assessments.OrderByDescending(a => a.AssessedAtUtc)
                .Select(a => new AssessmentResultDto(a.Id, a.Score, a.AssessorId, a.AssessedAtUtc))
                .ToList());
    }
}

public sealed record GetTrainingQueueQuery(
    Guid? TraineeId = null, bool IncludeCompleted = false,
    int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IQuery<Contracts.Common.PagedResponse<TrainingAssignmentDto>>;

public sealed class GetTrainingQueueHandler(IAppDbContext db)
    : IQueryHandler<GetTrainingQueueQuery, Contracts.Common.PagedResponse<TrainingAssignmentDto>>
{
    public async Task<Contracts.Common.PagedResponse<TrainingAssignmentDto>> Handle(
        GetTrainingQueueQuery q, CancellationToken ct)
    {
        var query = db.TrainingAssignments.AsNoTracking();
        if (q.TraineeId is { } trainee)
        {
            query = query.Where(t => t.TraineeId == trainee);
        }

        if (!q.IncludeCompleted)
        {
            query = query.Where(t => !t.Completed);
        }

        // API-004: pagination envelope — no silent cap; the client sees the total.
        return await query
            .OrderBy(t => t.DueDate)
            .Select(t => new TrainingAssignmentDto(
                t.Id, t.TraineeId, t.Subject, t.DocumentId, t.DueDate, t.Completed, t.CompletedAtUtc))
            .ToPagedAsync(PageRequest.Normalized(q.Page, q.PageSize), ct);
    }
}
