using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Authorization;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Roles &amp; privileges administration: the permission catalogue, the tenant's
/// roles, and their grants. Reading is a <c>roles.view</c> privilege; every
/// change is <c>roles.manage</c> — the one permission the lockout guard refuses
/// to let a tenant revoke from its last holder.
/// </summary>
[ApiController]
[Route("api/roles")]
[Authorize]
public sealed class RolesController(ISender sender) : ControllerBase
{
    /// <summary>The permission catalogue the matrix renders. Same for every tenant.</summary>
    [HttpGet("catalog")]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.View)]
    public async Task<IActionResult> Catalog(CancellationToken ct) =>
        Ok(await sender.Send(new GetPermissionCatalogQuery(), ct));

    [HttpGet]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.View)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetRolesQuery(), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetRoleQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken ct) =>
        Ok(new
        {
            id = await sender.Send(new CreateRoleCommand(
                request.Name, request.Description, request.PermissionKeys, request.DefaultLanguage), ct),
        });

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
    public async Task<IActionResult> Update(Guid id, UpdateRoleRequest request, CancellationToken ct)
    {
        await sender.Send(new UpdateRoleCommand(id, request.Name, request.Description, request.DefaultLanguage), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/permissions")]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
    public async Task<IActionResult> SetPermissions(Guid id, SetRolePermissionsRequest request, CancellationToken ct)
    {
        await sender.Send(new SetRolePermissionsCommand(id, request.PermissionKeys, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new SetRoleActiveCommand(id, Active: false), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission(PermissionCatalog.RolesPrivileges, PermissionAction.Manage)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new SetRoleActiveCommand(id, Active: true), ct);
        return NoContent();
    }
}
