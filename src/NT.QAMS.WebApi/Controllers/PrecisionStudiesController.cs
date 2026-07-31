using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Imprecision studies — within-run / between-run / within-lab (CLSI EP05).</summary>
[ApiController]
[Route("api/precision-studies")]
[Authorize]
public sealed class PrecisionStudiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetPrecisionStudiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetPrecisionStudyByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreatePrecisionStudyRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreatePrecisionStudyCommand(
            request.Analyte, request.Unit, request.Level,
            request.ClaimedRepeatabilityCvPct, request.ClaimedWithinLabCvPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/measurements")]
    public async Task<IActionResult> AddMeasurement(Guid id, AddPrecisionMeasurementRequest request, CancellationToken ct) =>
        Ok(new
        {
            measurementId = await sender.Send(new AddPrecisionMeasurementCommand(id, request.RunLabel, request.Value), ct),
        });

    [HttpPost("{id:guid}/measurements/import")]
    public async Task<IActionResult> ImportMeasurements(Guid id, ImportPrecisionMeasurementsRequest request, CancellationToken ct) =>
        Ok(await sender.Send(new ImportPrecisionMeasurementsCommand(id, request.Rows), ct));

    [HttpDelete("{id:guid}/measurements/{measurementId:guid}")]
    public async Task<IActionResult> RemoveMeasurement(Guid id, Guid measurementId, CancellationToken ct)
    {
        await sender.Send(new RemovePrecisionMeasurementCommand(id, measurementId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculatePrecisionCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, CancellationToken ct)
    {
        await sender.Send(new SignOffPrecisionCommand(id), ct);
        return NoContent();
    }
}
