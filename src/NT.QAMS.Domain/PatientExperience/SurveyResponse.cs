using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.PatientExperience;

/// <summary>One answer within a survey response: a 1–5 score for a question.</summary>
public sealed class SurveyAnswer : Entity
{
    internal SurveyAnswer(Guid questionId, int score)
    {
        QuestionId = questionId;
        Score = score;
    }

    private SurveyAnswer() { }

    public Guid QuestionId { get; private set; }
    public int Score { get; private set; }
}

/// <summary>
/// One patient's response to a satisfaction survey (HQMS M11): the answers, optionally
/// attributed to a department and service line so results can be compared across the
/// organisation. A response is immutable once captured.
/// </summary>
public sealed class SurveyResponse : AggregateRoot, ITenantScoped
{
    private readonly List<SurveyAnswer> _answers = [];

    private SurveyResponse() { }

    public Guid TenantId { get; set; }
    public Guid SurveyId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public string? ServiceLine { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }

    public IReadOnlyList<SurveyAnswer> Answers => _answers.AsReadOnly();

    /// <summary>
    /// Captures a response. Answers are (questionId, score) pairs; each score must be on the
    /// 1–5 scale and there must be at least one. The caller validates the question ids against
    /// the survey and that the survey is open.
    /// </summary>
    public static SurveyResponse Submit(
        Guid surveyId, Guid? departmentId, string? serviceLine,
        IReadOnlyList<(Guid QuestionId, int Score)> answers, DateTimeOffset at)
    {
        if (surveyId == Guid.Empty)
        {
            throw new DomainException("SVR-001", "A survey is required.");
        }

        if (answers is null || answers.Count == 0)
        {
            throw new DomainException("SVR-002", "A response needs at least one answer.");
        }

        foreach (var (questionId, score) in answers)
        {
            if (questionId == Guid.Empty)
            {
                throw new DomainException("SVR-003", "Each answer must reference a question.");
            }

            if (score is < SatisfactionSurvey.MinScore or > SatisfactionSurvey.MaxScore)
            {
                throw new DomainException("SVR-004", "Each answer score must be on the 1–5 scale.");
            }
        }

        if (answers.Select(a => a.QuestionId).Distinct().Count() != answers.Count)
        {
            throw new DomainException("SVR-005", "A question cannot be answered twice in one response.");
        }

        var response = new SurveyResponse
        {
            SurveyId = surveyId,
            DepartmentId = departmentId,
            ServiceLine = string.IsNullOrWhiteSpace(serviceLine) ? null : serviceLine.Trim(),
            SubmittedAtUtc = at,
        };
        response._answers.AddRange(answers.Select(a => new SurveyAnswer(a.QuestionId, a.Score)));
        return response;
    }
}

public sealed record SurveyResponseSubmitted(Guid ResponseId, Guid SurveyId) : DomainEvent;
