using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Facility;
using NT.QAMS.Contracts.Facility;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Environmental & facility monitoring (ISO 17025 §6.3): points, limits, readings.</summary>
[ApiController]
[Route("api/monitoring-points")]
[Authorize]
public sealed class MonitoringPointsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetMonitoringPointsQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMonitoringPointByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> Register(RegisterMonitoringPointRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RegisterMonitoringPointCommand(
            request.Name, request.Location, request.Parameter, request.Unit,
            request.LowLimit, request.HighLimit, request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/limits")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> SetLimits(Guid id, SetMonitoringLimitsRequest request, CancellationToken ct)
    {
        await sender.Send(new SetMonitoringLimitsCommand(id, request.LowLimit, request.HighLimit), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/readings")]
    public async Task<IActionResult> RecordReading(Guid id, RecordReadingRequest request, CancellationToken ct) =>
        Ok(new { readingId = await sender.Send(new RecordReadingCommand(id, request.Value, request.Remark), ct) });

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        await sender.Send(new SuspendMonitoringPointCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/resume")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        await sender.Send(new ResumeMonitoringPointCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireMonitoringPointCommand(id), ct);
        return NoContent();
    }
}
