using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Improvement;
using NT.QAMS.Contracts.Improvement;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>General feedback & satisfaction beyond formal complaints (ISO 17025 §8.6.2).</summary>
[ApiController]
[Route("api/feedback")]
[Authorize]
public sealed class FeedbackController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? type, CancellationToken ct) =>
        Ok(await sender.Send(new GetFeedbackQuery(status, type), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetFeedbackByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Log(LogFeedbackRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new LogFeedbackCommand(
            request.Source, request.Channel, request.Type, request.Subject, request.Details,
            request.SatisfactionScore, request.ReceivedOn, request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/review")]
    [RequirePermission(PermissionCatalog.Feedback, PermissionAction.Edit)]
    public async Task<IActionResult> Review(Guid id, ReviewFeedbackRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviewFeedbackCommand(id, request.ReviewNotes), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.Feedback, PermissionAction.Void)]
    public async Task<IActionResult> Close(Guid id, CloseFeedbackRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseFeedbackCommand(id, request.ActionSummary), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/escalate")]
    [RequirePermission(PermissionCatalog.Feedback, PermissionAction.Edit)]
    public async Task<IActionResult> Escalate(Guid id, EscalateFeedbackRequest request, CancellationToken ct) =>
        Ok(new
        {
            complaintId = await sender.Send(new EscalateFeedbackCommand(
                id, request.ComplainantName, request.ComplainantContact), ct),
        });
}
