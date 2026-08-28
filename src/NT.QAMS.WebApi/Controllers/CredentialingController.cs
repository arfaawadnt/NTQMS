using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Credentialing;
using NT.QAMS.Contracts.Credentialing;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Credentialing;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Credentialing &amp; Privileging API (HQMS M13): practitioner credentialing with primary-source
/// verification of licences, privilege delineation (request → grant/deny), the appointment /
/// reappointment / suspension lifecycle, a tiered licence-expiry register, and the point-of-care
/// privilege-verification check.
/// </summary>
[ApiController]
[Route("api/credentialing")]
[Authorize]
public sealed class CredentialingController(ISender sender) : ControllerBase
{
    [HttpGet("practitioners")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? specialty, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetPractitionersQuery(specialty, status), ct));

    [HttpGet("practitioners/{id:guid}")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetPractitionerByIdQuery(id), ct));

    [HttpGet("expiring")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.View)]
    public async Task<IActionResult> Expiring([FromQuery] int withinDays = 90, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetExpiringCredentialsQuery(withinDays), ct));

    /// <summary>Point-of-care check: does the practitioner hold the named privilege as an active grant today?</summary>
    [HttpGet("practitioners/{id:guid}/verify-privilege")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.View)]
    public async Task<IActionResult> VerifyPrivilege(Guid id, [FromQuery] string privilege, CancellationToken ct) =>
        Ok(await sender.Send(new VerifyPrivilegeQuery(id, privilege), ct));

    [HttpPost("practitioners")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Create)]
    public async Task<IActionResult> Register(RegisterPractitionerRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RegisterPractitionerCommand(request.FullName, request.Specialty), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("practitioners/{id:guid}/licences")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Edit)]
    public async Task<IActionResult> AddLicence(Guid id, AddLicenceRequest request, CancellationToken ct)
    {
        var licenceId = await sender.Send(new AddLicenceCommand(
            id, RequestEnum.Parse<CredentialType>(request.Type), request.Identifier, request.Issuer, request.ExpiresOn), ct);
        return Ok(new { id = licenceId });
    }

    [HttpPost("practitioners/{id:guid}/licences/{licenceId:guid}/verify")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Approve)]
    public async Task<IActionResult> VerifyLicence(Guid id, Guid licenceId, VerifyLicenceRequest request, CancellationToken ct)
    {
        await sender.Send(new VerifyLicenceCommand(id, licenceId, request.Source), ct);
        return NoContent();
    }

    [HttpPost("practitioners/{id:guid}/privileges")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Edit)]
    public async Task<IActionResult> RequestPrivilege(Guid id, RequestPrivilegeRequest request, CancellationToken ct)
    {
        var privilegeId = await sender.Send(new RequestPrivilegeCommand(id, request.Name), ct);
        return Ok(new { id = privilegeId });
    }

    [HttpPost("practitioners/{id:guid}/privileges/{privilegeId:guid}/grant")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Approve)]
    public async Task<IActionResult> GrantPrivilege(Guid id, Guid privilegeId, GrantPrivilegeRequest request, CancellationToken ct)
    {
        await sender.Send(new GrantPrivilegeCommand(id, privilegeId, request.GrantedUntil), ct);
        return NoContent();
    }

    [HttpPost("practitioners/{id:guid}/privileges/{privilegeId:guid}/deny")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Approve)]
    public async Task<IActionResult> DenyPrivilege(Guid id, Guid privilegeId, DenyPrivilegeRequest request, CancellationToken ct)
    {
        await sender.Send(new DenyPrivilegeCommand(id, privilegeId, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("practitioners/{id:guid}/credential")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Approve)]
    public async Task<IActionResult> Credential(Guid id, CredentialRequest request, CancellationToken ct)
    {
        await sender.Send(new CredentialPractitionerCommand(id, request.AppointedUntil), ct);
        return NoContent();
    }

    [HttpPost("practitioners/{id:guid}/reappoint")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Approve)]
    public async Task<IActionResult> Reappoint(Guid id, CredentialRequest request, CancellationToken ct)
    {
        await sender.Send(new ReappointPractitionerCommand(id, request.AppointedUntil), ct);
        return NoContent();
    }

    [HttpPost("practitioners/{id:guid}/suspend")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Void)]
    public async Task<IActionResult> Suspend(Guid id, SuspendPractitionerRequest request, CancellationToken ct)
    {
        await sender.Send(new SuspendPractitionerCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("practitioners/{id:guid}/reinstate")]
    [RequirePermission(PermissionCatalog.Credentialing, PermissionAction.Edit)]
    public async Task<IActionResult> Reinstate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ReinstatePractitionerCommand(id), ct);
        return NoContent();
    }
}
