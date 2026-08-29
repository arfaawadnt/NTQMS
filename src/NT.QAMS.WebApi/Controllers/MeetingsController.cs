using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Committees;
using NT.QAMS.Contracts.Committees;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Committee meetings API (HQMS M17): schedule a meeting, build its agenda, record
/// attendance, hold it once quorate, capture decisions/action items, and approve minutes.
/// Governed by the Committees module permissions.
/// </summary>
[ApiController]
[Route("api/meetings")]
[Authorize]
public sealed class MeetingsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMeetingByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Create)]
    public async Task<IActionResult> Schedule(ScheduleMeetingRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ScheduleMeetingCommand(request.CommitteeId, request.ScheduledAtUtc), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/agenda")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> AddAgendaItem(Guid id, AddAgendaItemRequest request, CancellationToken ct)
    {
        var itemId = await sender.Send(new AddAgendaItemCommand(
            id, request.Title, request.Detail, request.SourceRef, request.CarriedForward), ct);
        return Ok(new { itemId });
    }

    [HttpPost("{id:guid}/attendance")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> RecordAttendance(Guid id, RecordAttendanceRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordAttendanceCommand(id, request.UserId, request.Present), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/hold")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Approve)]
    public async Task<IActionResult> Hold(Guid id, CancellationToken ct)
    {
        await sender.Send(new HoldMeetingCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/decisions")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> AddDecision(Guid id, AddDecisionRequest request, CancellationToken ct)
    {
        var decisionId = await sender.Send(new AddDecisionCommand(id, request.Description, request.OwnerId, request.DueDate), ct);
        return Ok(new { decisionId });
    }

    [HttpPost("{id:guid}/decisions/{decisionId:guid}/close")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> CloseDecision(Guid id, Guid decisionId, CloseDecisionRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseDecisionCommand(id, decisionId, request.Note), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/minutes")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> RecordMinutes(Guid id, RecordMinutesRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordMinutesCommand(id, request.Minutes), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve-minutes")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Sign)]
    public async Task<IActionResult> ApproveMinutes(Guid id, ApproveMinutesRequest request, CancellationToken ct)
    {
        await sender.Send(new ApproveMinutesCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}
