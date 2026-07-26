using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Improvement.Commands;
using NT.QAMS.Application.Improvement.Queries;
using NT.QAMS.Contracts.Improvement;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// NC/CAPA workflow API. Transitions are verbs-as-subresources mapping 1:1 to
/// commands. Role gates follow the seed role-permission matrix; the fine-grained
/// privilege catalog replaces these attributes in full Phase 1.
/// </summary>
[ApiController]
[Route("api/nonconformances")]
[Authorize]
public sealed class NonconformancesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? eventType,
        CancellationToken ct) =>
        Ok(await sender.Send(new GetNcsQuery(status, search, eventType), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetNcByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Raise(RaiseNcRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RaiseNcCommand(
            request.Title, request.Description, request.Severity, request.Likelihood,
            Enum.Parse<NcSourceType>(request.SourceType, ignoreCase: true),
            request.BranchId, request.DepartmentId,
            Enum.Parse<QualityEventType>(request.EventType, ignoreCase: true)), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await sender.Send(new SubmitNcCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/triage")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Triage(Guid id, TriageNcRequest request, CancellationToken ct)
    {
        await sender.Send(new TriageNcCommand(id, request.AssigneeId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Reject(Guid id, RejectNcRequest request, CancellationToken ct)
    {
        await sender.Send(new RejectNcCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/rca")]
    public async Task<IActionResult> RecordRca(Guid id, RecordRcaRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordRcaCommand(
            id, Enum.Parse<RcaMethod>(request.Method, ignoreCase: true), request.Analysis), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/actions")]
    public async Task<IActionResult> PlanAction(Guid id, PlanCapaActionRequest request, CancellationToken ct)
    {
        var actionId = await sender.Send(new PlanCapaActionCommand(
            id, Enum.Parse<CapaActionType>(request.Type, ignoreCase: true),
            request.Details, request.OwnerId, request.DueDate), ct);
        return Ok(new { actionId });
    }

    [HttpPost("{id:guid}/actions/{actionId:guid}/complete")]
    public async Task<IActionResult> CompleteAction(Guid id, Guid actionId, CancellationToken ct)
    {
        await sender.Send(new CompleteCapaActionCommand(id, actionId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit-verification")]
    public async Task<IActionResult> SubmitForVerification(Guid id, CancellationToken ct)
    {
        await sender.Send(new SubmitNcForVerificationCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Verify(Guid id, VerifyNcRequest request, CancellationToken ct)
    {
        await sender.Send(new VerifyNcCommand(id, request.Passed), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/confirm-effectiveness")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> ConfirmEffectiveness(
        Guid id, ConfirmEffectivenessRequest request, CancellationToken ct)
    {
        await sender.Send(new ConfirmNcEffectivenessCommand(id, request.Effective), ct);
        return NoContent();
    }
}
