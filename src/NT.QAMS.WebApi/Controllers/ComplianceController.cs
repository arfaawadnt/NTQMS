using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.ComplianceLedger;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Read access to the compliance ledgers: the tamper-evident audit trail, the
/// electronic-signature log, security events, and on-demand chain verification.
/// Read-only by construction — the ledgers are append-only and appended to by
/// the event pipeline, never by an API call.
/// </summary>
[ApiController]
[Route("api/compliance")]
[Authorize(Roles = "QualityManager,TenantAdmin,ExternalAuditor")]
public sealed class ComplianceController(ISender sender) : ControllerBase
{
    [HttpGet("audit-trail")]
    public async Task<IActionResult> AuditTrail(
        [FromQuery] string? subject, [FromQuery] int take = 200, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetAuditTrailQuery(subject, take), ct));

    [HttpGet("field-changes")]
    public async Task<IActionResult> FieldChanges(
        [FromQuery] string? entityId, [FromQuery] int take = 200, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetFieldChangesQuery(entityId, take), ct));

    [HttpGet("signatures")]
    public async Task<IActionResult> Signatures([FromQuery] int take = 200, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetSignatureLogQuery(take), ct));

    [HttpGet("security-events")]
    public async Task<IActionResult> SecurityEvents([FromQuery] int take = 200, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetSecurityEventsQuery(take), ct));

    [HttpGet("chain-verification")]
    public async Task<IActionResult> VerifyChain(CancellationToken ct)
    {
        var tenantClaim = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            return BadRequest("Chain verification runs within a tenant context.");
        }

        return Ok(await sender.Send(new VerifyChainQuery(tenantId), ct));
    }
}
