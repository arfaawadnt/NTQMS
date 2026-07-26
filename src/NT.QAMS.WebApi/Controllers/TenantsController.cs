using MediatR;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Tenancy.Commands;
using NT.QAMS.Application.Tenancy.Queries;
using NT.QAMS.Contracts.Tenancy;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Control-plane tenant administration. Thin by law: bind → send → map.</summary>
[ApiController]
[Route("api/tenants")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.PlatformAdmin)]
public sealed class TenantsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Provision(
        ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await sender.Send(
            new ProvisionTenantCommand(
                request.Identifier, request.Name,
                request.AdminEmail, request.AdminDisplayName, request.AdminPassword),
            cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = tenantId }, new { id = tenantId });
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetTenantsQuery(), cancellationToken));
}
