using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.PatientSafety;
using NT.QAMS.Contracts.PatientSafety;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.PatientSafety;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Patient Safety API (HQMS M08): the falls and pressure-injury programmes. Events are
/// reported, reviewed and closed; rates are computed per 1,000 patient-days using the
/// ADT-derived denominator from the integration hub (M24).
/// </summary>
[ApiController]
[Route("api/patient-safety")]
[Authorize]
public sealed class PatientSafetyController(ISender sender) : ControllerBase
{
    [HttpGet("events")]
    [RequirePermission(PermissionCatalog.PatientSafety, PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? type, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetSafetyEventsQuery(type, status), ct));

    [HttpGet("events/{id:guid}")]
    [RequirePermission(PermissionCatalog.PatientSafety, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSafetyEventByIdQuery(id), ct));

    /// <summary>Falls and pressure-injury rates per 1,000 patient-days over the window.</summary>
    [HttpGet("rates")]
    [RequirePermission(PermissionCatalog.PatientSafety, PermissionAction.View)]
    public async Task<IActionResult> Rates([FromQuery] int windowDays = 30, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetSafetyRatesQuery(windowDays), ct));

    [HttpPost("falls")]
    [RequirePermission(PermissionCatalog.PatientSafety, PermissionAction.Create)]
    public async Task<IActionResult> ReportFall(ReportFallRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ReportFallCommand(
            request.PatientRef, request.Unit, request.OccurredAtUtc,
            RequestEnum.Parse<HarmLevel>(request.Harm), request.Description, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("pressure-injuries")]
    [RequirePermission(PermissionCatalog.PatientSafety, PermissionAction.Create)]
    public async Task<IActionResult> ReportPressureInjury(ReportPressureInjuryRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ReportPressureInjuryCommand(
            request.PatientRef, request.Unit, request.OccurredAtUtc,
            RequestEnum.Parse<HarmLevel>(request.Harm), request.Description,
            RequestEnum.Parse<PressureInjuryStage>(request.Stage),
            RequestEnum.Parse<InjuryOrigin>(request.Origin), request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("events/{id:guid}/review")]
    [RequirePermission(PermissionCatalog.PatientSafety, PermissionAction.Edit)]
    public async Task<IActionResult> Review(Guid id, ReviewSafetyEventRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviewSafetyEventCommand(id, request.Notes), ct);
        return NoContent();
    }

    [HttpPost("events/{id:guid}/close")]
    [RequirePermission(PermissionCatalog.PatientSafety, PermissionAction.Void)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseSafetyEventCommand(id), ct);
        return NoContent();
    }
}
