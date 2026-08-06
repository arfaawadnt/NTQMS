using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Annual PT/EQA participation plan (ISO 17025 §7.7.2).</summary>
[ApiController]
[Route("api/pt-plans")]
[Authorize]
public sealed class PtPlansController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetPtPlansQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetPtPlanByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.ProficiencyTesting, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreatePtPlanRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreatePtPlanCommand(request.Year), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/items")]
    [RequirePermission(PermissionCatalog.ProficiencyTesting, PermissionAction.Edit)]
    public async Task<IActionResult> AddItem(Guid id, AddPtPlanItemRequest request, CancellationToken ct) =>
        Ok(new
        {
            itemId = await sender.Send(new AddPtPlanItemCommand(
                id, request.Scheme, request.Analyte, request.Provider, request.PlannedCycles, request.Notes), ct),
        });

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [RequirePermission(PermissionCatalog.ProficiencyTesting, PermissionAction.Edit)]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken ct)
    {
        await sender.Send(new RemovePtPlanItemCommand(id, itemId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(PermissionCatalog.ProficiencyTesting, PermissionAction.Sign)]
    public async Task<IActionResult> Approve(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new ApprovePtPlanCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/fulfilments")]
    public async Task<IActionResult> RecordFulfilment(Guid id, RecordPtPlanFulfilmentRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordPtPlanFulfilmentCommand(id, request.ItemId, request.EnrollmentId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.ProficiencyTesting, PermissionAction.Void)]
    public async Task<IActionResult> Close(Guid id, ClosePtPlanRequest request, CancellationToken ct)
    {
        await sender.Send(new ClosePtPlanCommand(id, request.ClosureSummary), ct);
        return NoContent();
    }
}
