using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.IdentityAccess.Commands;
using NT.QAMS.Contracts.IdentityAccess;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Tenant user administration. Tenant-admin only. Scoped to the caller's tenant
/// by the handlers (the current tenant comes from the JWT). Enables onboarding
/// staff — the prerequisite for exercising segregation-of-duties workflows.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = "TenantAdmin")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetUsersQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new RegisterUserCommand(
            request.Email, request.DisplayName, request.Role, request.InitialPassword), ct) });

    [HttpPost("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken ct)
    {
        await sender.Send(new ChangeUserRoleCommand(id, request.Role), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new SetUserActiveCommand(id, Active: false), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new SetUserActiveCommand(id, Active: true), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetUserPasswordRequest request, CancellationToken ct)
    {
        await sender.Send(new ResetUserPasswordCommand(id, request.NewPassword), ct);
        return NoContent();
    }
}
