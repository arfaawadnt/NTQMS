using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Equipment;
using NT.QAMS.Contracts.Resources;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/equipment")]
[Authorize]
public sealed class EquipmentController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetEquipmentQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetEquipmentByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Register(RegisterEquipmentRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RegisterEquipmentCommand(
            request.Name, request.SerialNumber, request.Location,
            request.CalibrationIntervalDays, request.GracePeriodDays,
            request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/calibrations")]
    public async Task<IActionResult> LogCalibration(
        Guid id, LogCalibrationRequest request, CancellationToken ct)
    {
        await sender.Send(new LogCalibrationCommand(
            id, request.PerformedAt, request.Provider, request.Result, request.CertificateFileId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/maintenance")]
    public async Task<IActionResult> LogMaintenance(
        Guid id, LogMaintenanceRequest request, CancellationToken ct)
    {
        await sender.Send(new LogMaintenanceCommand(id, request.PerformedAt, request.WorkDescription), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireEquipmentCommand(id), ct);
        return NoContent();
    }
}
