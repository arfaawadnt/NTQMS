using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Measurement-uncertainty budgets (ISO 17025 §7.6 / ISO 15189 §7.3.4).</summary>
[ApiController]
[Route("api/uncertainty-budgets")]
[Authorize]
public sealed class UncertaintyController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetUncertaintyBudgetsQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetUncertaintyBudgetByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateUncertaintyBudgetRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateUncertaintyBudgetCommand(
            request.Analyte, request.Method, request.Unit, request.Level,
            request.CoverageFactor, request.TargetExpandedUncertainty), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/components")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Edit)]
    public async Task<IActionResult> AddComponent(Guid id, AddUncertaintyComponentRequest request, CancellationToken ct) =>
        Ok(new { componentId = await sender.Send(new AddUncertaintyComponentCommand(
            id, request.Name, request.Type, request.RelativeStandardUncertainty, request.Source), ct) });

    [HttpDelete("{id:guid}/components/{componentId:guid}")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Edit)]
    public async Task<IActionResult> RemoveComponent(Guid id, Guid componentId, CancellationToken ct)
    {
        await sender.Send(new RemoveUncertaintyComponentCommand(id, componentId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Edit)]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateUncertaintyBudgetCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> Approve(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new ApproveUncertaintyBudgetCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}
