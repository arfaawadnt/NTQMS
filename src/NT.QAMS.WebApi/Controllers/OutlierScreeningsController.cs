using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Automated outlier detection &amp; data normalisation (Tukey + modified-z).</summary>
[ApiController]
[Route("api/outlier-screenings")]
[Authorize]
public sealed class OutlierScreeningsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetOutlierScreeningsQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetOutlierScreeningByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Create(CreateOutlierScreeningRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateOutlierScreeningCommand(r.Dataset, r.Unit), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/points")]
    public async Task<IActionResult> AddPoint(Guid id, AddOutlierPointRequest r, CancellationToken ct) =>
        Ok(new { pointId = await sender.Send(new AddOutlierPointCommand(id, r.Value, r.Label), ct) });

    [HttpDelete("{id:guid}/points/{pointId:guid}")]
    public async Task<IActionResult> RemovePoint(Guid id, Guid pointId, CancellationToken ct)
    {
        await sender.Send(new RemoveOutlierPointCommand(id, pointId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateOutlierScreeningCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> SignOff(Guid id, CancellationToken ct)
    {
        await sender.Send(new SignOffOutlierScreeningCommand(id), ct);
        return NoContent();
    }
}
