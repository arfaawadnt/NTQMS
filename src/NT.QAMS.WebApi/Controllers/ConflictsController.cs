using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.RiskGovernance;
using NT.QAMS.Contracts.Governance;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Impartiality / conflict-of-interest register (ISO 17025 §4.1).</summary>
[ApiController]
[Route("api/conflicts")]
[Authorize]
public sealed class ConflictsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetConflictsQuery(status), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetConflictByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Declare(DeclareConflictRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new DeclareConflictCommand(
            request.DeclarantId, request.Description, request.RelatedParty, request.DeclaredOn), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/assess")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> Assess(Guid id, AssessConflictRequest request, CancellationToken ct)
    {
        await sender.Send(new AssessConflictCommand(id, request.RiskLevel, request.Mitigation), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> Close(Guid id, CloseConflictRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseConflictCommand(id, request.Outcome, request.ClosureNote), ct);
        return NoContent();
    }
}
