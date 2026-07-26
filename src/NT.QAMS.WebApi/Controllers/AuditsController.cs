using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AuditManagement.Commands;
using NT.QAMS.Application.AuditManagement.Queries;
using NT.QAMS.Contracts.AuditManagement;
using NT.QAMS.Domain.AuditManagement;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/audits")]
[Authorize]
public sealed class AuditsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditsQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Schedule(ScheduleAuditRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ScheduleAuditCommand(
            request.Title,
            Enum.Parse<AuditType>(request.Type, ignoreCase: true),
            request.LeadAuditorId,
            request.PlannedDate,
            request.Checklist.Select(i => (i.IsoClause, i.Question)).ToList(),
            request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        await sender.Send(new StartAuditCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/checklist/{itemId:guid}/answer")]
    public async Task<IActionResult> Answer(
        Guid id, Guid itemId, AnswerChecklistItemRequest request, CancellationToken ct)
    {
        await sender.Send(new AnswerChecklistItemCommand(
            id, itemId,
            Enum.Parse<ChecklistVerdict>(request.Verdict, ignoreCase: true),
            request.Evidence), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/findings")]
    public async Task<IActionResult> RaiseFinding(Guid id, RaiseFindingRequest request, CancellationToken ct)
    {
        var findingId = await sender.Send(new RaiseFindingCommand(
            id, Enum.Parse<FindingGrade>(request.Grade, ignoreCase: true), request.Description), ct);
        return Ok(new { findingId });
    }

    [HttpPost("{id:guid}/sign-off")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> SignOff(Guid id, CancellationToken ct)
    {
        await sender.Send(new SignOffAuditCommand(id), ct);
        return NoContent();
    }
}
