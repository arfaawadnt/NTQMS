using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Interference / analytical-specificity studies (CLSI EP07).</summary>
[ApiController]
[Route("api/interference-studies")]
[Authorize]
public sealed class InterferenceStudiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetInterferenceStudiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetInterferenceStudyByIdQuery(id), ct));

    /// <summary>Part 11 §11.50 signature manifest for this study, visible to any viewer of the record.</summary>
    [HttpGet("{id:guid}/signatures")]
    public async Task<IActionResult> Signatures(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new NT.QAMS.Application.ComplianceLedger.GetSignaturesForSubjectQuery($"INT:{id:N}"), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateInterferenceStudyRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateInterferenceStudyCommand(r.Analyte, r.Unit, r.AllowableBiasPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/measurements")]
    public async Task<IActionResult> AddMeasurement(Guid id, AddInterferenceMeasurementRequest r, CancellationToken ct) =>
        Ok(new { measurementId = await sender.Send(new AddInterferenceMeasurementCommand(id, r.Kind, r.Interferent, r.Value), ct) });

    [HttpDelete("{id:guid}/measurements/{measurementId:guid}")]
    public async Task<IActionResult> RemoveMeasurement(Guid id, Guid measurementId, CancellationToken ct)
    {
        await sender.Send(new RemoveInterferenceMeasurementCommand(id, measurementId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateInterferenceCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new SignOffInterferenceCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}
