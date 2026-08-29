using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.EnvironmentOfCare;
using NT.QAMS.Contracts.EnvironmentOfCare;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.EnvironmentOfCare;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Environment of Care &amp; Emergency Preparedness API (HQMS M15): environmental safety rounds with
/// findings, emergency drills (scheduled → executed → evaluated), and the EOC summary dashboard.
/// </summary>
[ApiController]
[Route("api/eoc")]
[Authorize]
public sealed class EnvironmentOfCareController(ISender sender) : ControllerBase
{
    // ── Safety rounds ──────────────────────────────────────────────────────────
    [HttpGet("rounds")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.View)]
    public async Task<IActionResult> ListRounds([FromQuery] string? type, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetSafetyRoundsQuery(type, status), ct));

    [HttpGet("rounds/{id:guid}")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.View)]
    public async Task<IActionResult> GetRound(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSafetyRoundByIdQuery(id), ct));

    [HttpGet("summary")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.View)]
    public async Task<IActionResult> Summary(CancellationToken ct) =>
        Ok(await sender.Send(new GetEocSummaryQuery(), ct));

    [HttpPost("rounds")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Create)]
    public async Task<IActionResult> ScheduleRound(ScheduleRoundRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ScheduleRoundCommand(
            request.Area, RequestEnum.Parse<RoundType>(request.Type), request.ScheduledDate), ct);
        return CreatedAtAction(nameof(GetRound), new { id }, new { id });
    }

    [HttpPost("rounds/{id:guid}/start")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
    public async Task<IActionResult> StartRound(Guid id, CancellationToken ct)
    {
        await sender.Send(new StartRoundCommand(id), ct);
        return NoContent();
    }

    [HttpPost("rounds/{id:guid}/findings")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
    public async Task<IActionResult> AddFinding(Guid id, AddFindingRequest request, CancellationToken ct)
    {
        var findingId = await sender.Send(new AddFindingCommand(
            id, request.Description, RequestEnum.Parse<FindingSeverity>(request.Severity)), ct);
        return Ok(new { id = findingId });
    }

    [HttpPost("rounds/{id:guid}/findings/{findingId:guid}/resolve")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
    public async Task<IActionResult> ResolveFinding(Guid id, Guid findingId, ResolveFindingRequest request, CancellationToken ct)
    {
        await sender.Send(new ResolveFindingCommand(id, findingId, request.Note), ct);
        return NoContent();
    }

    // M-22: hand a safety-round finding off into the corrective-action pipeline.
    // Gated on NC.create — creating a CAPA is a nonconformance act — mirroring the
    // command policy, so the HTTP and application tiers agree.
    [HttpPost("rounds/{id:guid}/findings/{findingId:guid}/raise-nc")]
    [RequirePermission(PermissionCatalog.Nonconformances, PermissionAction.Create)]
    public async Task<IActionResult> RaiseNcFromFinding(Guid id, Guid findingId, CancellationToken ct)
    {
        var ncId = await sender.Send(new RaiseNcFromRoundFindingCommand(id, findingId), ct);
        return Ok(new { id = ncId });
    }

    [HttpPost("rounds/{id:guid}/complete")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Void)]
    public async Task<IActionResult> CompleteRound(Guid id, CancellationToken ct)
    {
        await sender.Send(new CompleteRoundCommand(id), ct);
        return NoContent();
    }

    // ── Drills ─────────────────────────────────────────────────────────────────
    [HttpGet("drills")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.View)]
    public async Task<IActionResult> ListDrills([FromQuery] string? type, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetDrillsQuery(type, status), ct));

    [HttpGet("drills/{id:guid}")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.View)]
    public async Task<IActionResult> GetDrill(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetDrillByIdQuery(id), ct));

    [HttpPost("drills")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Create)]
    public async Task<IActionResult> ScheduleDrill(ScheduleDrillRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ScheduleDrillCommand(
            RequestEnum.Parse<DrillType>(request.Type), request.Location, request.ScheduledDate), ct);
        return CreatedAtAction(nameof(GetDrill), new { id }, new { id });
    }

    [HttpPost("drills/{id:guid}/execute")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Edit)]
    public async Task<IActionResult> ExecuteDrill(Guid id, ExecuteDrillRequest request, CancellationToken ct)
    {
        await sender.Send(new ExecuteDrillCommand(id, request.ExecutedAtUtc, request.ParticipantCount), ct);
        return NoContent();
    }

    [HttpPost("drills/{id:guid}/evaluate")]
    [RequirePermission(PermissionCatalog.EnvironmentOfCare, PermissionAction.Approve)]
    public async Task<IActionResult> EvaluateDrill(Guid id, EvaluateDrillRequest request, CancellationToken ct)
    {
        await sender.Send(new EvaluateDrillCommand(id, request.Score, request.ImprovementNotes), ct);
        return NoContent();
    }
}
