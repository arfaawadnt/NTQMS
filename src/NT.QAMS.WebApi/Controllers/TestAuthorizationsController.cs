using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Competency;
using NT.QAMS.Contracts.Resources;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Personnel authorization matrix — who may perform/release/train which test (ISO 17025 §6.2.6).</summary>
[ApiController]
[Route("api/test-authorizations")]
[Authorize]
public sealed class TestAuthorizationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? userId, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetTestAuthorizationsQuery(userId, status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetTestAuthorizationByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Grant(GrantTestAuthorizationRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new GrantTestAuthorizationCommand(
            request.UserId, request.TestCatalogItemId, request.CompetencyRecordId, request.Scope), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Suspend(Guid id, SuspendTestAuthorizationRequest request, CancellationToken ct)
    {
        await sender.Send(new SuspendTestAuthorizationCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reinstate")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Reinstate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ReinstateTestAuthorizationCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Revoke(Guid id, RevokeTestAuthorizationRequest request, CancellationToken ct)
    {
        await sender.Send(new RevokeTestAuthorizationCommand(id, request.Reason), ct);
        return NoContent();
    }
}
