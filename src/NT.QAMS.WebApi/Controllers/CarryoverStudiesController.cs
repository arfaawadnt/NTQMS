using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Sample-carryover studies (CLSI EP10-style).</summary>
[ApiController]
[Route("api/carryover-studies")]
[Authorize]
public sealed class CarryoverStudiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetCarryoverStudiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCarryoverStudyByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Create(CreateCarryoverStudyRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateCarryoverStudyCommand(r.Analyte, r.Unit, r.AllowableCarryoverPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/readings")]
    public async Task<IActionResult> AddReading(Guid id, AddCarryoverReadingRequest r, CancellationToken ct) =>
        Ok(new { readingId = await sender.Send(new AddCarryoverReadingCommand(id, r.Kind, r.Sequence, r.Value), ct) });

    [HttpDelete("{id:guid}/readings/{readingId:guid}")]
    public async Task<IActionResult> RemoveReading(Guid id, Guid readingId, CancellationToken ct)
    {
        await sender.Send(new RemoveCarryoverReadingCommand(id, readingId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateCarryoverCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> SignOff(Guid id, CancellationToken ct)
    {
        await sender.Send(new SignOffCarryoverCommand(id), ct);
        return NoContent();
    }
}
