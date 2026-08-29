using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.InfectionControl;
using NT.QAMS.Contracts.InfectionControl;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.InfectionControl;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Infection Prevention &amp; Control API (HQMS M09): healthcare-associated infection surveillance
/// (CLABSI, CAUTI, VAP, SSI) and the device-exposure register. Device-associated rates are computed
/// per 1,000 device-days, with device-utilisation ratios against the M24 ADT patient-days.
/// </summary>
[ApiController]
[Route("api/infection-control")]
[Authorize]
public sealed class InfectionControlController(ISender sender) : ControllerBase
{
    // ── HAI cases ─────────────────────────────────────────────────────────────
    [HttpGet("cases")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.View)]
    public async Task<IActionResult> ListCases(
        [FromQuery] string? type, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetHaiCasesQuery(type, status, page, pageSize), ct));

    [HttpGet("cases/{id:guid}")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.View)]
    public async Task<IActionResult> GetCase(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetHaiCaseByIdQuery(id), ct));

    /// <summary>Device-associated infection rates per 1,000 device-days over the window.</summary>
    [HttpGet("rates")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.View)]
    public async Task<IActionResult> Rates([FromQuery] int windowDays = 30, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetHaiRatesQuery(windowDays), ct));

    [HttpPost("cases")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.Create)]
    public async Task<IActionResult> ReportCase(ReportHaiCaseRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ReportHaiCaseCommand(
            RequestEnum.Parse<HaiType>(request.Type), request.PatientRef, request.Unit,
            request.OnsetDateUtc, request.Organism, request.Description, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetCase), new { id }, new { id });
    }

    [HttpPost("cases/{id:guid}/review")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.Edit)]
    public async Task<IActionResult> ReviewCase(Guid id, ReviewHaiCaseRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviewHaiCaseCommand(id, request.Notes), ct);
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/close")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.Void)]
    public async Task<IActionResult> CloseCase(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseHaiCaseCommand(id), ct);
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/reject")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.Void)]
    public async Task<IActionResult> RejectCase(Guid id, RejectHaiCaseRequest request, CancellationToken ct)
    {
        await sender.Send(new RejectHaiCaseCommand(id, request.Reason), ct);
        return NoContent();
    }

    // ── Device exposures (the device-day denominator) ──────────────────────────
    [HttpGet("devices")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.View)]
    public async Task<IActionResult> ListDevices(
        [FromQuery] string? deviceType, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetDeviceExposuresQuery(deviceType, status, page, pageSize), ct));

    [HttpPost("devices")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.Create)]
    public async Task<IActionResult> RecordDevice(RecordDeviceExposureRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RecordDeviceExposureCommand(
            request.PatientRef, request.Unit, RequestEnum.Parse<DeviceType>(request.DeviceType),
            request.InsertedAtUtc, request.DepartmentId), ct);
        return CreatedAtAction(nameof(ListDevices), new { }, new { id });
    }

    [HttpPost("devices/{id:guid}/remove")]
    [RequirePermission(PermissionCatalog.InfectionControl, PermissionAction.Edit)]
    public async Task<IActionResult> RemoveDevice(Guid id, RemoveDeviceRequest request, CancellationToken ct)
    {
        await sender.Send(new RemoveDeviceCommand(id, request.RemovedAtUtc), ct);
        return NoContent();
    }
}
