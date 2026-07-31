using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.IdentityAccess.Commands;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Tenant user administration, gated by the configurable <c>users</c> privileges
/// (view to list, manage to change). Scoped to the caller's tenant by the
/// handlers (the current tenant comes from the JWT). Enables onboarding staff —
/// the prerequisite for exercising segregation-of-duties workflows.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.View)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetUsersQuery(), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new RegisterUserCommand(
            request.Email, request.DisplayName, request.Role, request.InitialPassword, request.RoleId), ct) });

    [HttpPost("{id:guid}/role")]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken ct)
    {
        await sender.Send(new ChangeUserRoleCommand(id, request.Role), ct);
        return NoContent();
    }

    /// <summary>Moves the user onto a configurable role.</summary>
    [HttpPut("{id:guid}/assigned-role")]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> AssignRole(Guid id, AssignUserRoleRequest request, CancellationToken ct)
    {
        await sender.Send(new AssignUserRoleCommand(id, request.RoleId), ct);
        return NoContent();
    }

    /// <summary>Sets the user's allowed branches/departments. Empty lists mean unrestricted.</summary>
    [HttpPut("{id:guid}/scope")]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> SetScope(Guid id, SetUserScopeRequest request, CancellationToken ct)
    {
        await sender.Send(new SetUserScopeCommand(id, request.BranchIds, request.DepartmentIds), ct);
        return NoContent();
    }

    /// <summary>Sets the user's interface language; null inherits role, then tenant.</summary>
    [HttpPut("{id:guid}/language")]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> SetLanguage(Guid id, SetUserLanguageRequest request, CancellationToken ct)
    {
        await sender.Send(new SetUserLanguageCommand(id, request.Language), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new SetUserActiveCommand(id, Active: false), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new SetUserActiveCommand(id, Active: true), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(PermissionCatalog.Users, PermissionAction.Manage)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetUserPasswordRequest request, CancellationToken ct)
    {
        await sender.Send(new ResetUserPasswordCommand(id, request.NewPassword), ct);
        return NoContent();
    }
}
