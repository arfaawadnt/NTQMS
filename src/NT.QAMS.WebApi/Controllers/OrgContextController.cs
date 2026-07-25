using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Organization;
using NT.QAMS.Contracts.Platform;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Organizational context: interested parties + internal/external issues (ISO 9001 §4.1/§4.2).</summary>
[ApiController]
[Route("api/org-context")]
[Authorize]
public sealed class OrgContextController(ISender sender) : ControllerBase
{
    // ── Interested parties ───────────────────────────────────────────────────

    [HttpGet("interested-parties")]
    public async Task<IActionResult> Parties(CancellationToken ct) =>
        Ok(await sender.Send(new GetInterestedPartiesQuery(), ct));

    [HttpPost("interested-parties")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> RegisterParty(RegisterInterestedPartyRequest request, CancellationToken ct) =>
        Ok(new
        {
            id = await sender.Send(new RegisterInterestedPartyCommand(
                request.Name, request.Category, request.NeedsAndExpectations,
                request.RelevantRequirements, request.ReviewedOn), ct),
        });

    [HttpPut("interested-parties/{id:guid}")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> ReviseParty(Guid id, ReviseInterestedPartyRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviseInterestedPartyCommand(
            id, request.Name, request.Category, request.NeedsAndExpectations,
            request.RelevantRequirements, request.ReviewedOn), ct);
        return NoContent();
    }

    [HttpPost("interested-parties/{id:guid}/archive")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> ArchiveParty(Guid id, CancellationToken ct)
    {
        await sender.Send(new ArchiveInterestedPartyCommand(id), ct);
        return NoContent();
    }

    // ── Context issues ───────────────────────────────────────────────────────

    [HttpGet("issues")]
    public async Task<IActionResult> Issues(CancellationToken ct) =>
        Ok(await sender.Send(new GetContextIssuesQuery(), ct));

    [HttpPost("issues")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> RegisterIssue(RegisterContextIssueRequest request, CancellationToken ct) =>
        Ok(new
        {
            id = await sender.Send(new RegisterContextIssueCommand(
                request.Type, request.Category, request.Description, request.Impact), ct),
        });

    [HttpPut("issues/{id:guid}")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> ReviseIssue(Guid id, ReviseContextIssueRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviseContextIssueCommand(
            id, request.Type, request.Category, request.Description, request.Impact), ct);
        return NoContent();
    }

    [HttpPost("issues/{id:guid}/link-risk")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> LinkRisk(Guid id, LinkContextIssueRiskRequest request, CancellationToken ct)
    {
        await sender.Send(new LinkContextIssueRiskCommand(id, request.RiskId), ct);
        return NoContent();
    }

    [HttpPost("issues/{id:guid}/close")]
    [Authorize(Roles = "QualityManager,DepartmentHead,TenantAdmin")]
    public async Task<IActionResult> CloseIssue(Guid id, CloseContextIssueRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseContextIssueCommand(id, request.Resolution), ct);
        return NoContent();
    }
}
