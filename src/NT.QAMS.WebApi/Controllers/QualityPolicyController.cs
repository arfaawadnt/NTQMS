using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Improvement;
using NT.QAMS.Contracts.Improvement;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// The controlled quality policy (ISO 9001 §5.2 / ISO 17025 §8.2): the current
/// statement is readable by any authenticated user (it must be communicated), while
/// drafting and approval are restricted to quality management. Approval carries a
/// segregation-of-duties guard (the approver cannot be the author).
/// </summary>
[ApiController]
[Route("api/quality-policy")]
[Authorize]
public sealed class QualityPolicyController(ISender sender) : ControllerBase
{
    /// <summary>The policy currently in force (Active), or 204 if none has been approved yet.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct)
    {
        var policy = await sender.Send(new GetActiveQualityPolicyQuery(), ct);
        return policy is null ? NoContent() : Ok(policy);
    }

    /// <summary>The full version history (all drafts, active, and superseded).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.QualityPolicy, PermissionAction.View)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetQualityPoliciesQuery(), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.QualityPolicy, PermissionAction.Create)]
    public async Task<IActionResult> Draft(DraftQualityPolicyRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new DraftQualityPolicyCommand(request.Statement), ct) });

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCatalog.QualityPolicy, PermissionAction.Edit)]
    public async Task<IActionResult> Revise(Guid id, ReviseQualityPolicyRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviseQualityPolicyCommand(id, request.Statement), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(PermissionCatalog.QualityPolicy, PermissionAction.Approve)]
    public async Task<IActionResult> Approve(Guid id, ApproveQualityPolicyRequest request, CancellationToken ct)
    {
        await sender.Send(new ApproveQualityPolicyCommand(id, request.EffectiveDate), ct);
        return NoContent();
    }
}
