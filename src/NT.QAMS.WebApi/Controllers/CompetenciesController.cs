using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Competency;
using NT.QAMS.Contracts.Resources;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/competencies")]
[Authorize]
public sealed class CompetenciesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? traineeId, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetCompetenciesQuery(traineeId, status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCompetencyByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Assign(AssignCompetencyRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new AssignCompetencyCommand(
            request.TraineeId, request.Subject, request.DocumentId, request.ValidityMonths), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/assessments")]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Score(Guid id, ScoreAssessmentRequest request, CancellationToken ct)
    {
        await sender.Send(new ScoreAssessmentCommand(id, request.Score), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/authorize")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> AuthorizeCompetency(Guid id, CancellationToken ct)
    {
        await sender.Send(new AuthorizeCompetencyCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Revoke(Guid id, RevokeCompetencyRequest request, CancellationToken ct)
    {
        await sender.Send(new RevokeCompetencyCommand(id, request.Reason), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/training-assignments")]
[Authorize]
public sealed class TrainingAssignmentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Queue(
        [FromQuery] Guid? traineeId, [FromQuery] bool includeCompleted, CancellationToken ct) =>
        Ok(await sender.Send(new GetTrainingQueueQuery(traineeId, includeCompleted), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Assign(AssignTrainingRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new AssignTrainingCommand(
            request.TraineeId, request.Subject, request.DocumentId, request.DueDate), ct);
        return Ok(new { id });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await sender.Send(new CompleteTrainingCommand(id), ct);
        return NoContent();
    }
}
