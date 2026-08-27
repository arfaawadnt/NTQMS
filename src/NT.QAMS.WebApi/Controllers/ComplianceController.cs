using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
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
[Authorize]
[Route("api/compliance")]
[RequirePermission(PermissionCatalog.Compliance, PermissionAction.View)]
public sealed class ComplianceController(ISender sender) : ControllerBase
{
    [HttpGet("audit-trail")]
    public async Task<IActionResult> AuditTrail(
        [FromQuery] string? subject, [FromQuery] int take = 200, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetAuditTrailQuery(subject, take), ct));

    /// <summary>
    /// The audit trail for a single record (its detail-page timeline): entries the
    /// record produced, matched on the aggregate id — never a payload substring, so
    /// a record's trail never shows another record's logs.
    /// </summary>
    [HttpGet("audit-trail/record/{subjectId:guid}")]
    public async Task<IActionResult> RecordAuditTrail(
        Guid subjectId, [FromQuery] int take = 200, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetRecordAuditTrailQuery(subjectId, take), ct));

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

    [HttpGet("audit-trail-reviews")]
    public async Task<IActionResult> AuditTrailReviews(CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditTrailReviewsQuery(), ct));

    [HttpPost("audit-trail-reviews")]
    [RequirePermission(PermissionCatalog.Compliance, PermissionAction.Create)]
    public async Task<IActionResult> OpenAuditTrailReview(
        NT.QAMS.Contracts.Compliance.OpenAuditTrailReviewRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new OpenAuditTrailReviewCommand(request.PeriodStart, request.PeriodEnd), ct) });

    [HttpPost("audit-trail-reviews/{id:guid}/complete")]
    [RequirePermission(PermissionCatalog.Compliance, PermissionAction.Sign)]
    public async Task<IActionResult> CompleteAuditTrailReview(
        Guid id, NT.QAMS.Contracts.Compliance.CompleteAuditTrailReviewRequest request, CancellationToken ct)
    {
        await sender.Send(
            new CompleteAuditTrailReviewCommand(id, request.AnomaliesFound, request.Conclusion, request.Password, request.Pin), ct);
        return NoContent();
    }

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
