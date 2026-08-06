using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Detection capability — LoB / LoD / LoQ studies (CLSI EP17).</summary>
[ApiController]
[Route("api/detection-limit-studies")]
[Authorize]
public sealed class DetectionLimitStudiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetDetectionLimitStudiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetDetectionLimitStudyByIdQuery(id), ct));

    /// <summary>Part 11 §11.50 signature manifest for this study, visible to any viewer of the record.</summary>
    [HttpGet("{id:guid}/signatures")]
    public async Task<IActionResult> Signatures(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new NT.QAMS.Application.ComplianceLedger.GetSignaturesForSubjectQuery($"DL:{id:N}"), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateDetectionLimitStudyRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateDetectionLimitStudyCommand(
            request.Analyte, request.Unit, request.Method, request.LoqCvTargetPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/measurements")]
    public async Task<IActionResult> AddMeasurement(Guid id, AddDetectionMeasurementRequest request, CancellationToken ct) =>
        Ok(new
        {
            measurementId = await sender.Send(new AddDetectionMeasurementCommand(
                id, request.Kind, request.AssignedValue, request.MeasuredValue), ct),
        });

    [HttpDelete("{id:guid}/measurements/{measurementId:guid}")]
    public async Task<IActionResult> RemoveMeasurement(Guid id, Guid measurementId, CancellationToken ct)
    {
        await sender.Send(new RemoveDetectionMeasurementCommand(id, measurementId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateDetectionLimitCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new SignOffDetectionLimitCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}
