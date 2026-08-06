using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Linearity / analytical-measurement-range verification (CLSI EP06).</summary>
[ApiController]
[Route("api/linearity-studies")]
[Authorize]
public sealed class LinearityStudiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetLinearityStudiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetLinearityStudyByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateLinearityStudyRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateLinearityStudyCommand(
            request.Analyte, request.Unit, request.Method, request.AllowableDeviationPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/measurements")]
    public async Task<IActionResult> AddMeasurement(Guid id, AddLinearityMeasurementRequest request, CancellationToken ct) =>
        Ok(new
        {
            measurementId = await sender.Send(new AddLinearityMeasurementCommand(
                id, request.AssignedValue, request.MeasuredValue), ct),
        });

    [HttpDelete("{id:guid}/measurements/{measurementId:guid}")]
    public async Task<IActionResult> RemoveMeasurement(Guid id, Guid measurementId, CancellationToken ct)
    {
        await sender.Send(new RemoveLinearityMeasurementCommand(id, measurementId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateLinearityCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new SignOffLinearityCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}
