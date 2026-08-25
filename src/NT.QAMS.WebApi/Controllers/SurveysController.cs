using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.PatientExperience;
using NT.QAMS.Contracts.PatientExperience;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Patient satisfaction surveys API (HQMS M11): define a survey and its questions, open it
/// for responses, capture responses by department/service line, and read the scored results
/// by question, domain and department.
/// </summary>
[ApiController]
[Route("api/surveys")]
[Authorize]
public sealed class SurveysController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetSurveysQuery(status), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSurveyByIdQuery(id), ct));

    /// <summary>Scored results: overall, by question, by domain and by department.</summary>
    [HttpGet("{id:guid}/results")]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.View)]
    public async Task<IActionResult> Results(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSurveyResultsQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateSurveyRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateSurveyCommand(request.Title, request.Description), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/questions")]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.Create)]
    public async Task<IActionResult> AddQuestion(Guid id, AddSurveyQuestionRequest request, CancellationToken ct)
    {
        var questionId = await sender.Send(new AddSurveyQuestionCommand(id, request.Text, request.Domain), ct);
        return Ok(new { questionId });
    }

    [HttpPost("{id:guid}/open")]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.Approve)]
    public async Task<IActionResult> Open(Guid id, CancellationToken ct)
    {
        await sender.Send(new OpenSurveyCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.Void)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseSurveyCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/responses")]
    [RequirePermission(PermissionCatalog.Surveys, PermissionAction.Create)]
    public async Task<IActionResult> SubmitResponse(Guid id, SubmitSurveyResponseRequest request, CancellationToken ct)
    {
        var answers = (request.Answers ?? [])
            .Select(a => (a.QuestionId, a.Score))
            .ToList();
        var responseId = await sender.Send(
            new SubmitSurveyResponseCommand(id, request.DepartmentId, request.ServiceLine, answers), ct);
        return Ok(new { responseId });
    }
}
