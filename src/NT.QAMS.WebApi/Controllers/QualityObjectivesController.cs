using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Improvement;
using NT.QAMS.Contracts.Improvement;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Quality objectives & targets (ISO 9001 §6.2 / ISO 17025 §8.2).</summary>
[ApiController]
[Route("api/quality-objectives")]
[Authorize]
public sealed class QualityObjectivesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetQualityObjectivesQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetQualityObjectiveByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> Define(DefineQualityObjectiveRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new DefineQualityObjectiveCommand(
            request.Title, request.Description, request.Metric, request.Unit,
            request.TargetValue, request.Direction, request.OwnerId,
            request.PeriodStart, request.PeriodEnd, request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/progress")]
    public async Task<IActionResult> RecordProgress(Guid id, RecordObjectiveProgressRequest request, CancellationToken ct) =>
        Ok(new
        {
            updateId = await sender.Send(new RecordObjectiveProgressCommand(
                id, request.MeasuredOn, request.Value, request.Comment), ct),
        });

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> Close(Guid id, CloseObjectiveRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseObjectiveCommand(id, request.Outcome, request.Note), ct);
        return NoContent();
    }
}
