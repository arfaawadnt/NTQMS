using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
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
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetEquipmentQuery(status, page, pageSize), ct));

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
        await sender.Send(new LogMaintenanceCommand(id, request.PerformedAt, request.WorkDescription, request.CertificateFileId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/checks")]
    public async Task<IActionResult> RecordIntermediateCheck(
        Guid id, RecordIntermediateCheckRequest request, CancellationToken ct) =>
        Ok(new
        {
            checkId = await sender.Send(new RecordIntermediateCheckCommand(
                id, request.PerformedOn, request.CheckType, request.Passed,
                request.ReferenceStandardId, request.Remarks), ct),
        });

    [HttpPost("{id:guid}/retire")]
    [RequirePermission(PermissionCatalog.Equipment, PermissionAction.Void)]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireEquipmentCommand(id), ct);
        return NoContent();
    }

    // ── Downtime & availability (HQMS M14) ──────────────────────────────────────
    [HttpPost("{id:guid}/downtime")]
    [RequirePermission(PermissionCatalog.Equipment, PermissionAction.Edit)]
    public async Task<IActionResult> StartDowntime(Guid id, StartDowntimeRequest request, CancellationToken ct)
    {
        var downtimeId = await sender.Send(new StartDowntimeCommand(
            id, request.StartedAtUtc,
            Enum.Parse<NT.QAMS.Domain.Equipment.DowntimeCategory>(request.Category, ignoreCase: true), request.Reason), ct);
        return Ok(new { id = downtimeId });
    }

    [HttpPost("{id:guid}/downtime/{downtimeId:guid}/end")]
    [RequirePermission(PermissionCatalog.Equipment, PermissionAction.Edit)]
    public async Task<IActionResult> EndDowntime(Guid id, Guid downtimeId, EndDowntimeRequest request, CancellationToken ct)
    {
        await sender.Send(new EndDowntimeCommand(id, downtimeId, request.EndedAtUtc), ct);
        return NoContent();
    }

    // ── Recalls & field safety notices (HQMS M14) ───────────────────────────────
    [HttpGet("safety-notices")]
    [RequirePermission(PermissionCatalog.Equipment, PermissionAction.View)]
    public async Task<IActionResult> OpenSafetyNotices(CancellationToken ct) =>
        Ok(await sender.Send(new GetOpenSafetyNoticesQuery(), ct));

    [HttpPost("{id:guid}/safety-notices")]
    [RequirePermission(PermissionCatalog.Equipment, PermissionAction.Edit)]
    public async Task<IActionResult> LogSafetyNotice(Guid id, LogSafetyNoticeRequest request, CancellationToken ct)
    {
        var noticeId = await sender.Send(new LogSafetyNoticeCommand(
            id, Enum.Parse<NT.QAMS.Domain.Equipment.SafetyNoticeType>(request.Type, ignoreCase: true),
            request.Reference, request.Issuer,
            Enum.Parse<NT.QAMS.Domain.Equipment.SafetyNoticeSeverity>(request.Severity, ignoreCase: true),
            request.ReceivedOn, request.RequiredActionBy), ct);
        return Ok(new { id = noticeId });
    }

    [HttpPost("{id:guid}/safety-notices/{noticeId:guid}/action")]
    [RequirePermission(PermissionCatalog.Equipment, PermissionAction.Edit)]
    public async Task<IActionResult> ActionSafetyNotice(Guid id, Guid noticeId, ActionSafetyNoticeRequest request, CancellationToken ct)
    {
        await sender.Send(new ActionSafetyNoticeCommand(id, noticeId, request.Note, request.On), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/safety-notices/{noticeId:guid}/close")]
    [RequirePermission(PermissionCatalog.Equipment, PermissionAction.Void)]
    public async Task<IActionResult> CloseSafetyNotice(Guid id, Guid noticeId, CancellationToken ct)
    {
        await sender.Send(new CloseSafetyNoticeCommand(id, noticeId), ct);
        return NoContent();
    }
}
