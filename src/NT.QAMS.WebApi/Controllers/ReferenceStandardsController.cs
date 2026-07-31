using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Equipment;
using NT.QAMS.Contracts.Resources;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Reference standard / CRM register — metrological traceability (ISO 17025 §6.5).</summary>
[ApiController]
[Route("api/reference-standards")]
[Authorize]
public sealed class ReferenceStandardsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetReferenceStandardsQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetReferenceStandardByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.ReferenceStandards, PermissionAction.Create)]
    public async Task<IActionResult> Register(RegisterReferenceStandardRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RegisterReferenceStandardCommand(
            request.Name, request.Type, request.TraceableTo,
            request.Manufacturer, request.LotNumber, request.CertificateNumber,
            request.CertifiedValue, request.UncertaintyStatement,
            request.ReceivedOn, request.ExpiresOn, request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/quarantine")]
    [RequirePermission(PermissionCatalog.ReferenceStandards, PermissionAction.Edit)]
    public async Task<IActionResult> Quarantine(Guid id, QuarantineReferenceStandardRequest request, CancellationToken ct)
    {
        await sender.Send(new QuarantineReferenceStandardCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission(PermissionCatalog.ReferenceStandards, PermissionAction.Approve)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ReactivateReferenceStandardCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/retire")]
    [RequirePermission(PermissionCatalog.ReferenceStandards, PermissionAction.Void)]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireReferenceStandardCommand(id), ct);
        return NoContent();
    }
}
