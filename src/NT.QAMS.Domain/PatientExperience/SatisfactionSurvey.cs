using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.PatientExperience;

/// <summary>Lifecycle of a satisfaction survey.</summary>
public enum SurveyStatus { Draft, Open, Closed }

/// <summary>
/// A question on a satisfaction survey, grouped into a domain (e.g. Communication, Waiting
/// time, Clinical care) so results can be scored by question and by domain. Answered on a
/// 1–5 Likert scale.
/// </summary>
public sealed class SurveyQuestion : Entity
{
    internal SurveyQuestion(string text, string domain, int displayOrder)
    {
        Text = text;
        Domain = domain;
        DisplayOrder = displayOrder;
    }

    private SurveyQuestion()
    {
        Text = null!;
        Domain = null!;
    }

    public string Text { get; private set; }

    /// <summary>The question domain/theme the answer rolls up into.</summary>
    public string Domain { get; private set; }

    public int DisplayOrder { get; private set; }
}

/// <summary>
/// A patient satisfaction survey (HQMS M11): the questions patients answer, grouped by
/// domain. Draft while it is built, Open while collecting responses, Closed when the
/// collection window ends. Responses are a separate aggregate; scoring aggregates them by
/// question, domain, department and service line.
/// </summary>
public sealed class SatisfactionSurvey : AggregateRoot, ITenantScoped
{
    /// <summary>The Likert scale answers use (1–5).</summary>
    public const int MinScore = 1;
    public const int MaxScore = 5;

    private readonly List<SurveyQuestion> _questions = [];

    private SatisfactionSurvey() { Title = null!; }

    public Guid TenantId { get; set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public SurveyStatus Status { get; private set; }

    public IReadOnlyList<SurveyQuestion> Questions => _questions.AsReadOnly();

    public static SatisfactionSurvey Create(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("SVY-001", "A survey title is required.");
        }

        return new SatisfactionSurvey
        {
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = SurveyStatus.Draft,
        };
    }

    public Guid AddQuestion(string text, string domain)
    {
        if (Status != SurveyStatus.Draft)
        {
            throw new InvalidStateTransitionException(
                "SVY-010", "Questions can only be added while the survey is in draft.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("SVY-011", "Question text is required.");
        }

        var question = new SurveyQuestion(
            text.Trim(), string.IsNullOrWhiteSpace(domain) ? "General" : domain.Trim(), _questions.Count + 1);
        _questions.Add(question);
        return question.Id;
    }

    public void Open()
    {
        if (Status != SurveyStatus.Draft)
        {
            throw new InvalidStateTransitionException("SVY-012", $"Cannot open a survey in state {Status}.");
        }

        if (_questions.Count == 0)
        {
            throw new DomainException("SVY-013", "A survey needs at least one question before it opens.");
        }

        Status = SurveyStatus.Open;
        Raise(new SurveyOpened(Id, Title, _questions.Count));
    }

    public void Close()
    {
        if (Status != SurveyStatus.Open)
        {
            throw new InvalidStateTransitionException("SVY-014", $"Cannot close a survey in state {Status}.");
        }

        Status = SurveyStatus.Closed;
        Raise(new SurveyClosed(Id, Title));
    }

    /// <summary>True when the survey accepts responses.</summary>
    public bool AcceptsResponses => Status == SurveyStatus.Open;

    /// <summary>The question ids that belong to this survey (for validating a response).</summary>
    public bool HasQuestion(Guid questionId) => _questions.Any(q => q.Id == questionId);
}

public sealed record SurveyOpened(Guid SurveyId, string Title, int QuestionCount) : DomainEvent;
public sealed record SurveyClosed(Guid SurveyId, string Title) : DomainEvent;
