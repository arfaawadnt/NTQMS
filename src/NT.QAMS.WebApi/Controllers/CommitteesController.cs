using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Committees;
using NT.QAMS.Contracts.Committees;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Committees;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Committees &amp; Governance API (HQMS M17): the committee register (terms of reference,
/// membership, quorum, frequency). Meetings are handled by the meetings endpoints.
/// </summary>
[ApiController]
[Route("api/committees")]
[Authorize]
public sealed class CommitteesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetCommitteesQuery(status), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCommitteeByIdQuery(id), ct));

    /// <summary>Every open action item across this committee's meetings.</summary>
    [HttpGet("{id:guid}/open-actions")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.View)]
    public async Task<IActionResult> OpenActions(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetOpenActionsQuery(id), ct));

    /// <summary>Meetings held (or scheduled) by this committee.</summary>
    [HttpGet("{id:guid}/meetings")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.View)]
    public async Task<IActionResult> Meetings(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMeetingsQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateCommitteeRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateCommitteeCommand(
            request.Name, request.TermsOfReference,
            RequestEnum.Parse<CommitteeFrequency>(request.Frequency), request.QuorumSize), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/members")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> AddMember(Guid id, AddCommitteeMemberRequest request, CancellationToken ct)
    {
        var memberId = await sender.Send(new AddCommitteeMemberCommand(id, request.UserId, request.RoleTitle), ct);
        return Ok(new { memberId });
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        await sender.Send(new RemoveCommitteeMemberCommand(id, memberId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/quorum")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateQuorum(Guid id, UpdateQuorumRequest request, CancellationToken ct)
    {
        await sender.Send(new UpdateQuorumCommand(id, request.QuorumSize), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/disband")]
    [RequirePermission(PermissionCatalog.Committees, PermissionAction.Void)]
    public async Task<IActionResult> Disband(Guid id, CancellationToken ct)
    {
        await sender.Send(new DisbandCommitteeCommand(id), ct);
        return NoContent();
    }
}
