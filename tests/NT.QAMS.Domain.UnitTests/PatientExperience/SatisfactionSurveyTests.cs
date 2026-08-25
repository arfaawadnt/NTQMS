using FluentAssertions;
using NT.QAMS.Domain.PatientExperience;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.PatientExperience;

public class SatisfactionSurveyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private static SatisfactionSurvey DraftWithQuestion(out Guid questionId)
    {
        var survey = SatisfactionSurvey.Create("Inpatient experience", "Post-discharge survey");
        questionId = survey.AddQuestion("Staff communicated clearly", "Communication");
        return survey;
    }

    [Fact]
    public void Cannot_open_a_survey_with_no_questions()
    {
        var survey = SatisfactionSurvey.Create("Empty", null);
        var act = survey.Open;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SVY-013");
    }

    [Fact]
    public void Questions_cannot_be_added_after_opening()
    {
        var survey = DraftWithQuestion(out _);
        survey.Open();
        var act = () => survey.AddQuestion("Another", "General");
        act.Should().Throw<InvalidStateTransitionException>().Which.Code.Should().Be("SVY-010");
    }

    [Fact]
    public void Open_then_closed_stops_accepting_responses()
    {
        var survey = DraftWithQuestion(out _);
        survey.AcceptsResponses.Should().BeFalse("a draft does not accept responses");

        survey.Open();
        survey.AcceptsResponses.Should().BeTrue();

        survey.Close();
        survey.AcceptsResponses.Should().BeFalse();
        survey.DomainEvents.OfType<SurveyClosed>().Should().ContainSingle();
    }

    // ── Responses ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_response_needs_at_least_one_answer()
    {
        var act = () => SurveyResponse.Submit(Guid.CreateVersion7(), null, null, [], Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SVR-002");
    }

    [Fact]
    public void Answer_scores_must_be_on_the_1_to_5_scale()
    {
        var q = Guid.CreateVersion7();
        var act = () => SurveyResponse.Submit(Guid.CreateVersion7(), null, null, [(q, 6)], Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SVR-004");
    }

    [Fact]
    public void A_question_cannot_be_answered_twice()
    {
        var q = Guid.CreateVersion7();
        var act = () => SurveyResponse.Submit(Guid.CreateVersion7(), null, null, [(q, 4), (q, 5)], Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("SVR-005");
    }

    [Fact]
    public void A_valid_response_captures_its_answers()
    {
        var q1 = Guid.CreateVersion7();
        var q2 = Guid.CreateVersion7();
        var dept = Guid.CreateVersion7();

        var response = SurveyResponse.Submit(Guid.CreateVersion7(), dept, "Cardiology", [(q1, 5), (q2, 3)], Now);

        response.Answers.Should().HaveCount(2);
        response.DepartmentId.Should().Be(dept);
        response.ServiceLine.Should().Be("Cardiology");
    }
}
