using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.RiskGovernance;
using NT.QAMS.Contracts.RiskGovernance;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// FMEA / HFMEA API (HQMS M04): prospective failure-mode analysis. Each failure mode is
/// scored Severity × Occurrence × Detection = RPN and worked highest-risk first; a
/// post-action re-score captures the improvement. Governed by the Risks module permissions.
/// </summary>
[ApiController]
[Route("api/fmea")]
[Authorize]
public sealed class FmeaController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetFmeasQuery(status), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetFmeaByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateFmeaRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateFmeaCommand(
            request.Title, request.ProcessName,
            RequestEnum.Parse<FmeaType>(request.Type), request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/failure-modes")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Create)]
    public async Task<IActionResult> AddFailureMode(Guid id, AddFailureModeRequest request, CancellationToken ct)
    {
        var modeId = await sender.Send(new AddFailureModeCommand(
            id, request.ProcessStep, request.FailureMode, request.Effect, request.Cause,
            request.Severity, request.Occurrence, request.Detection), ct);
        return Ok(new { modeId });
    }

    [HttpPost("{id:guid}/failure-modes/{modeId:guid}/recommend")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Edit)]
    public async Task<IActionResult> Recommend(Guid id, Guid modeId, RecommendActionRequest request, CancellationToken ct)
    {
        await sender.Send(new RecommendActionCommand(id, modeId, request.Action, request.OwnerId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/failure-modes/{modeId:guid}/residual")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Edit)]
    public async Task<IActionResult> RecordResidual(Guid id, Guid modeId, RecordResidualRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordFmeaResidualCommand(id, modeId, request.Severity, request.Occurrence, request.Detection), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Approve)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ActivateFmeaCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Void)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseFmeaCommand(id), ct);
        return NoContent();
    }
}
