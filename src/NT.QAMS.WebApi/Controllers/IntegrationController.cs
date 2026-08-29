using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Integration;
using NT.QAMS.Contracts.Integration;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Integration;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Integration &amp; Interoperability API (HQMS M24). Configures interface endpoints, receives
/// canonical ADT events from the protocol adapters into an idempotent inbox, maintains the
/// patient-stay projection (patient-day denominators), and serves the interface-monitoring
/// and reconciliation views. HL7 v2 / FHIR R4 wire adapters call the ingest endpoint.
/// </summary>
[ApiController]
[Route("api/integration")]
[Authorize]
public sealed class IntegrationController(ISender sender) : ControllerBase
{
    [HttpGet("endpoints")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.View)]
    public async Task<IActionResult> Endpoints(CancellationToken ct) =>
        Ok(await sender.Send(new GetEndpointsQuery(), ct));

    [HttpGet("endpoints/{id:guid}/messages")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.View)]
    public async Task<IActionResult> Messages(
        Guid id, [FromQuery] string? status, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetIntegrationMessagesQuery(id, status, take), ct));

    [HttpGet("reconciliation")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.View)]
    public async Task<IActionResult> Reconciliation(CancellationToken ct) =>
        Ok(await sender.Send(new GetReconciliationQuery(), ct));

    [HttpGet("census")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.View)]
    public async Task<IActionResult> Census([FromQuery] int windowDays = 30, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetPatientCensusQuery(windowDays), ct));

    [HttpPost("endpoints")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.Manage)]
    public async Task<IActionResult> Register(RegisterEndpointRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RegisterEndpointCommand(
            request.Name,
            RequestEnum.Parse<InterfaceSystem>(request.System),
            RequestEnum.Parse<InterfaceProtocol>(request.Protocol)), ct);
        return Ok(new { id });
    }

    [HttpPost("endpoints/{id:guid}/suspend")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.Manage)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        await sender.Send(new SuspendEndpointCommand(id), ct);
        return NoContent();
    }

    [HttpPost("endpoints/{id:guid}/resume")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.Manage)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        await sender.Send(new ResumeEndpointCommand(id), ct);
        return NoContent();
    }

    /// <summary>Ingests a canonical ADT event (called by the HL7/FHIR adapter). Idempotent by dedup key.</summary>
    [HttpPost("endpoints/{id:guid}/adt")]
    [RequirePermission(PermissionCatalog.Integration, PermissionAction.Create)]
    public async Task<IActionResult> IngestAdt(Guid id, IngestAdtEventRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new IngestAdtEventCommand(
            id, request.DedupKey, request.MessageType, request.RawPayload,
            request.EventType,
            request.PatientRef, request.EncounterRef, request.Unit, request.DepartmentId, request.EventAtUtc), ct);
        return Ok(result);
    }
}
