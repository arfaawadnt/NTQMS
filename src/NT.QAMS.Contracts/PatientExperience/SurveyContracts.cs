namespace NT.QAMS.Contracts.PatientExperience;

public sealed record CreateSurveyRequest(string Title, string? Description);
public sealed record AddSurveyQuestionRequest(string Text, string Domain);
public sealed record SurveyAnswerInput(Guid QuestionId, int Score);
public sealed record SubmitSurveyResponseRequest(
    Guid? DepartmentId, string? ServiceLine, IReadOnlyList<SurveyAnswerInput> Answers);

public sealed record SurveyQuestionDto(Guid Id, string Text, string Domain, int DisplayOrder);

public sealed record SurveyListItemDto(
    Guid Id, string Title, string Status, int QuestionCount, int ResponseCount, decimal? OverallScore);

public sealed record SurveyDetailDto(
    Guid Id, string Title, string? Description, string Status, IReadOnlyList<SurveyQuestionDto> Questions);

// ── Results / scoring ────────────────────────────────────────────────────────

public sealed record QuestionScoreDto(Guid QuestionId, string Text, string Domain, decimal MeanScore, int Answers);
public sealed record DomainScoreDto(string Domain, decimal MeanScore, int Answers);
public sealed record DepartmentScoreDto(Guid? DepartmentId, decimal MeanScore, int Responses);

public sealed record SurveyResultsDto(
    Guid SurveyId, string Title, string Status, int ResponseCount, decimal? OverallScore,
    IReadOnlyList<QuestionScoreDto> ByQuestion,
    IReadOnlyList<DomainScoreDto> ByDomain,
    IReadOnlyList<DepartmentScoreDto> ByDepartment);
