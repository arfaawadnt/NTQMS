using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AuditManagement;
using NT.QAMS.Contracts.AuditManagement;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Annual audit programme API (HQMS M05). Maintains the risk-based plan of audits for a
/// cycle, links each plan line to the scheduled audit that fulfils it, and serves the
/// coverage view so no area goes unaudited. Governed by the Audits module permissions.
/// </summary>
[ApiController]
[Route("api/audit-programs")]
[Authorize]
public sealed class AuditProgramsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditProgramsQuery(status), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditProgramByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateAuditProgramRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateAuditProgramCommand(request.Year, request.Title), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/plan")]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.Create)]
    public async Task<IActionResult> AddPlannedAudit(Guid id, AddPlannedAuditRequest request, CancellationToken ct)
    {
        var plannedId = await sender.Send(new AddPlannedAuditCommand(
            id, request.ScopeArea, request.DepartmentId, request.StandardChapter,
            RequestEnum.Parse<PlannedAuditPriority>(request.Priority), request.PlannedQuarter), ct);
        return Ok(new { plannedId });
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.Approve)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ActivateAuditProgramCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/plan/{plannedId:guid}/schedule")]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.Approve)]
    public async Task<IActionResult> LinkScheduled(
        Guid id, Guid plannedId, LinkScheduledAuditRequest request, CancellationToken ct)
    {
        await sender.Send(new LinkScheduledAuditCommand(id, plannedId, request.AuditId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/plan/{plannedId:guid}/complete")]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.Approve)]
    public async Task<IActionResult> CompletePlanned(
        Guid id, Guid plannedId, CompletePlannedAuditRequest request, CancellationToken ct)
    {
        await sender.Send(new CompletePlannedAuditCommand(id, plannedId, request.CompletedOn), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.Audits, PermissionAction.Void)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseAuditProgramCommand(id), ct);
        return NoContent();
    }
}
