using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Tenancy.Commands;
using NT.QAMS.Contracts.Tenancy;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// A tenant administrator's own security settings. Scoped to the caller's tenant
/// via the request context — a TenantAdmin can only read/change their own tenant.
/// </summary>
[ApiController]
[Route("api/tenant-settings")]
[Authorize]
[RequirePermission(PermissionCatalog.TenantSettings, PermissionAction.Manage)]
public sealed class TenantSettingsController(ISender sender) : ControllerBase
{
    /// <summary>Current privileged-MFA enforcement for this tenant (F-04).</summary>
    [HttpGet("mfa-policy")]
    public async Task<IActionResult> GetMfaPolicy(CancellationToken ct) =>
        Ok(new TenantMfaPolicyDto(await sender.Send(new GetTenantMfaPolicyQuery(), ct)));

    /// <summary>Enable or disable enforced MFA for this tenant's privileged users.</summary>
    [HttpPut("mfa-policy")]
    public async Task<IActionResult> SetMfaPolicy(SetTenantMfaPolicyRequest request, CancellationToken ct)
    {
        await sender.Send(new SetTenantMfaPolicyCommand(request.Require), ct);
        return NoContent();
    }
}
