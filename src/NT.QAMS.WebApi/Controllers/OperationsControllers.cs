using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
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
    public async Task<IActionResult> List(
        [FromQuery] string? state,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetArchivesQuery(state, page, pageSize), ct));

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
    [RequirePermission(PermissionCatalog.Records, PermissionAction.Void)]
    public async Task<IActionResult> Dispose(Guid id, CancellationToken ct)
    {
        await sender.Send(new DisposeRecordCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/legal-hold")]
    [RequirePermission(PermissionCatalog.Records, PermissionAction.Void)]
    public async Task<IActionResult> PlaceLegalHold(Guid id, PlaceLegalHoldRequest request, CancellationToken ct)
    {
        await sender.Send(new PlaceLegalHoldCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/legal-hold")]
    [RequirePermission(PermissionCatalog.Records, PermissionAction.Void)]
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
    [RequirePermission(PermissionCatalog.Tasks, PermissionAction.Manage)]
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
    public async Task<IActionResult> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        // The handler resolves the caller's roles from the database; the token's
        // tier claim is not passed in because it goes stale on role reassignment.
        Ok(await sender.Send(new GetMyTasksQuery(page, pageSize), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Tasks, PermissionAction.Create)]
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
