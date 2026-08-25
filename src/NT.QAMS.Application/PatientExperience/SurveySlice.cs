using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.PatientExperience;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.PatientExperience;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.PatientExperience;

// ── Commands ─────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Surveys, PermissionAction.Create)]
public sealed record CreateSurveyCommand(string Title, string? Description) : ICommand<Guid>;

public sealed class CreateSurveyValidator : AbstractValidator<CreateSurveyCommand>
{
    public CreateSurveyValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class CreateSurveyHandler(IAppDbContext db) : ICommandHandler<CreateSurveyCommand, Guid>
{
    public async Task<Guid> Handle(CreateSurveyCommand c, CancellationToken ct)
    {
        var survey = SatisfactionSurvey.Create(c.Title, c.Description);
        db.SatisfactionSurveys.Add(survey);
        await db.SaveChangesAsync(ct);
        return survey.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Surveys, PermissionAction.Create)]
public sealed record AddSurveyQuestionCommand(Guid SurveyId, string Text, string Domain) : ICommand<Guid>;

public sealed class AddSurveyQuestionValidator : AbstractValidator<AddSurveyQuestionCommand>
{
    public AddSurveyQuestionValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Domain).MaximumLength(100);
    }
}

public sealed class AddSurveyQuestionHandler(IAppDbContext db) : ICommandHandler<AddSurveyQuestionCommand, Guid>
{
    public async Task<Guid> Handle(AddSurveyQuestionCommand c, CancellationToken ct)
    {
        var survey = await Load(db, c.SurveyId, ct);
        var id = survey.AddQuestion(c.Text, c.Domain);
        await db.SaveChangesAsync(ct);
        return id;
    }

    internal static async Task<SatisfactionSurvey> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.SatisfactionSurveys.Include(s => s.Questions).SingleOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new DomainException("SVY-404", "Survey not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Surveys, PermissionAction.Approve)]
public sealed record OpenSurveyCommand(Guid SurveyId) : ICommand;

public sealed class OpenSurveyHandler(IAppDbContext db) : ICommandHandler<OpenSurveyCommand>
{
    public async Task Handle(OpenSurveyCommand c, CancellationToken ct)
    {
        (await AddSurveyQuestionHandler.Load(db, c.SurveyId, ct)).Open();
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Surveys, PermissionAction.Void)]
public sealed record CloseSurveyCommand(Guid SurveyId) : ICommand;

public sealed class CloseSurveyHandler(IAppDbContext db) : ICommandHandler<CloseSurveyCommand>
{
    public async Task Handle(CloseSurveyCommand c, CancellationToken ct)
    {
        (await AddSurveyQuestionHandler.Load(db, c.SurveyId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Surveys, PermissionAction.Create)]
public sealed record SubmitSurveyResponseCommand(
    Guid SurveyId, Guid? DepartmentId, string? ServiceLine,
    IReadOnlyList<(Guid QuestionId, int Score)> Answers) : ICommand<Guid>;

public sealed class SubmitSurveyResponseHandler(IAppDbContext db, IClock clock)
    : ICommandHandler<SubmitSurveyResponseCommand, Guid>
{
    public async Task<Guid> Handle(SubmitSurveyResponseCommand c, CancellationToken ct)
    {
        var survey = await AddSurveyQuestionHandler.Load(db, c.SurveyId, ct);
        if (!survey.AcceptsResponses)
        {
            throw new DomainException("SVR-010", "The survey is not open for responses.");
        }

        foreach (var (questionId, _) in c.Answers ?? [])
        {
            if (!survey.HasQuestion(questionId))
            {
                throw new DomainException("SVR-011", "An answer references a question not in this survey.");
            }
        }

        var response = SurveyResponse.Submit(c.SurveyId, c.DepartmentId, c.ServiceLine, c.Answers ?? [], clock.UtcNow);
        db.SurveyResponses.Add(response);
        await db.SaveChangesAsync(ct);
        return response.Id;
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetSurveysQuery(string? Status = null) : IQuery<IReadOnlyList<SurveyListItemDto>>;

public sealed class GetSurveysHandler(IAppDbContext db) : IQueryHandler<GetSurveysQuery, IReadOnlyList<SurveyListItemDto>>
{
    public async Task<IReadOnlyList<SurveyListItemDto>> Handle(GetSurveysQuery q, CancellationToken ct)
    {
        var query = db.SatisfactionSurveys.AsNoTracking().Include(s => s.Questions).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(s => s.Status.ToString() == q.Status);
        }

        var surveys = await query.OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        var ids = surveys.Select(s => s.Id).ToList();

        // Response counts and mean scores, computed from the answers of each survey's responses.
        var responseData = await db.SurveyResponses.AsNoTracking()
            .Where(r => ids.Contains(r.SurveyId))
            .Select(r => new { r.SurveyId, Scores = r.Answers.Select(a => a.Score).ToList() })
            .ToListAsync(ct);

        var bySurvey = responseData.GroupBy(r => r.SurveyId).ToDictionary(
            g => g.Key,
            g => (Count: g.Count(), Mean: MeanOrNull(g.SelectMany(x => x.Scores))));

        return surveys
            .Select(s => new SurveyListItemDto(
                s.Id, s.Title, s.Status.ToString(), s.Questions.Count,
                bySurvey.TryGetValue(s.Id, out var d) ? d.Count : 0,
                bySurvey.TryGetValue(s.Id, out var d2) ? d2.Mean : null))
            .ToList();
    }

    internal static decimal? MeanOrNull(IEnumerable<int> scores)
    {
        var list = scores.ToList();
        return list.Count == 0 ? null : decimal.Round((decimal)list.Average(), 2);
    }
}

public sealed record GetSurveyByIdQuery(Guid SurveyId) : IQuery<SurveyDetailDto>;

public sealed class GetSurveyByIdHandler(IAppDbContext db) : IQueryHandler<GetSurveyByIdQuery, SurveyDetailDto>
{
    public async Task<SurveyDetailDto> Handle(GetSurveyByIdQuery q, CancellationToken ct)
    {
        var s = await db.SatisfactionSurveys.AsNoTracking().Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.Id == q.SurveyId, ct)
            ?? throw new DomainException("SVY-404", "Survey not found.");

        return new SurveyDetailDto(
            s.Id, s.Title, s.Description, s.Status.ToString(),
            s.Questions.OrderBy(x => x.DisplayOrder)
                .Select(x => new SurveyQuestionDto(x.Id, x.Text, x.Domain, x.DisplayOrder)).ToList());
    }
}

/// <summary>
/// Scores the survey: overall mean and breakdowns by question, question domain and
/// department — the analysis M11 asks for (by department, service line and question domain).
/// </summary>
public sealed record GetSurveyResultsQuery(Guid SurveyId) : IQuery<SurveyResultsDto>;

public sealed class GetSurveyResultsHandler(IAppDbContext db) : IQueryHandler<GetSurveyResultsQuery, SurveyResultsDto>
{
    public async Task<SurveyResultsDto> Handle(GetSurveyResultsQuery q, CancellationToken ct)
    {
        var survey = await db.SatisfactionSurveys.AsNoTracking().Include(s => s.Questions)
            .SingleOrDefaultAsync(s => s.Id == q.SurveyId, ct)
            ?? throw new DomainException("SVY-404", "Survey not found.");

        var responses = await db.SurveyResponses.AsNoTracking()
            .Where(r => r.SurveyId == q.SurveyId)
            .Select(r => new
            {
                r.DepartmentId,
                Answers = r.Answers.Select(a => new { a.QuestionId, a.Score }).ToList(),
            })
            .ToListAsync(ct);

        var domainByQuestion = survey.Questions.ToDictionary(x => x.Id, x => x.Domain);
        var allAnswers = responses.SelectMany(r => r.Answers).ToList();

        var byQuestion = survey.Questions
            .OrderBy(x => x.DisplayOrder)
            .Select(x =>
            {
                var scores = allAnswers.Where(a => a.QuestionId == x.Id).Select(a => a.Score).ToList();
                return new QuestionScoreDto(x.Id, x.Text, x.Domain, GetSurveysHandler.MeanOrNull(scores) ?? 0m, scores.Count);
            })
            .ToList();

        var byDomain = allAnswers
            .GroupBy(a => domainByQuestion.TryGetValue(a.QuestionId, out var d) ? d : "General")
            .OrderBy(g => g.Key)
            .Select(g => new DomainScoreDto(g.Key, GetSurveysHandler.MeanOrNull(g.Select(a => a.Score)) ?? 0m, g.Count()))
            .ToList();

        var byDepartment = responses
            .GroupBy(r => r.DepartmentId)
            .Select(g => new DepartmentScoreDto(
                g.Key, GetSurveysHandler.MeanOrNull(g.SelectMany(r => r.Answers).Select(a => a.Score)) ?? 0m, g.Count()))
            .OrderByDescending(d => d.MeanScore)
            .ToList();

        return new SurveyResultsDto(
            survey.Id, survey.Title, survey.Status.ToString(), responses.Count,
            GetSurveysHandler.MeanOrNull(allAnswers.Select(a => a.Score)),
            byQuestion, byDomain, byDepartment);
    }
}
