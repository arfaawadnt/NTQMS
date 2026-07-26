using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Records;
using NT.QAMS.Application.Sla;
using NT.QAMS.Contracts.Operations;
using NT.QAMS.Domain.Records;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/archives")]
[Authorize]
public sealed class ArchivesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetArchivesQuery(state), ct));

    [HttpPost]
    public async Task<IActionResult> Archive(ArchiveRecordRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new ArchiveRecordCommand(
            request.SourceModule, request.SourceRef, request.SnapshotFileId,
            Enum.Parse<RetentionClass>(request.RetentionClass, ignoreCase: true)), ct) });

    [HttpPost("{id:guid}/retrieve")]
    public async Task<IActionResult> Retrieve(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetrieveRecordCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, CancellationToken ct)
    {
        await sender.Send(new ReturnRecordCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/dispose")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Dispose(Guid id, CancellationToken ct)
    {
        await sender.Send(new DisposeRecordCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/legal-hold")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> PlaceLegalHold(Guid id, PlaceLegalHoldRequest request, CancellationToken ct)
    {
        await sender.Send(new PlaceLegalHoldCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/legal-hold")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> ReleaseLegalHold(Guid id, CancellationToken ct)
    {
        await sender.Send(new ReleaseLegalHoldCommand(id), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/sla-definitions")]
[Authorize]
public sealed class SlaDefinitionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetSlaDefinitionsQuery(), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Upsert(UpsertSlaRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new UpsertSlaCommand(
            request.Module, request.Severity, request.TargetHours), ct) });
}

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class WorkTasksController(ISender sender) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return Ok(await sender.Send(new GetMyTasksQuery(role), ct));
    }

    [HttpPost]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new CreateTaskCommand(
            request.Subject, request.SubjectRef, request.AssigneeUserId,
            request.AssigneeRole, request.DueDate), ct) });

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await sender.Send(new CompleteTaskCommand(id), ct);
        return NoContent();
    }
}
