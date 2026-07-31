using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Method-comparison / patient-comparability studies (CLSI EP09).</summary>
[ApiController]
[Route("api/method-comparisons")]
[Authorize]
public sealed class MethodComparisonsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetMethodComparisonsQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMethodComparisonByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateMethodComparisonRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateMethodComparisonCommand(
            request.Analyte, request.Unit, request.ReferenceMethod, request.TestMethod), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/pairs")]
    public async Task<IActionResult> AddPair(Guid id, AddMeasurementPairRequest request, CancellationToken ct) =>
        Ok(new
        {
            pairId = await sender.Send(new AddMeasurementPairCommand(
                id, request.ReferenceValue, request.TestValue, request.SampleId), ct),
        });

    [HttpPost("{id:guid}/pairs/import")]
    public async Task<IActionResult> ImportPairs(Guid id, ImportMeasurementPairsRequest request, CancellationToken ct) =>
        Ok(await sender.Send(new ImportMeasurementPairsCommand(id, request.Rows), ct));

    [HttpDelete("{id:guid}/pairs/{pairId:guid}")]
    public async Task<IActionResult> RemovePair(Guid id, Guid pairId, CancellationToken ct)
    {
        await sender.Send(new RemoveMeasurementPairCommand(id, pairId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateMethodComparisonCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, CancellationToken ct)
    {
        await sender.Send(new SignOffMethodComparisonCommand(id), ct);
        return NoContent();
    }
}
