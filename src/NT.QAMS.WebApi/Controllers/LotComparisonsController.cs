using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Reagent/control lot-to-lot comparison studies.</summary>
[ApiController]
[Route("api/lot-comparisons")]
[Authorize]
public sealed class LotComparisonsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetLotComparisonsQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetLotComparisonByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Create(CreateLotComparisonRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateLotComparisonCommand(r.Analyte, r.Unit, r.CurrentLot, r.NewLot, r.AllowableBiasPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/pairs")]
    public async Task<IActionResult> AddPair(Guid id, AddLotPairRequest r, CancellationToken ct) =>
        Ok(new { pairId = await sender.Send(new AddLotPairCommand(id, r.CurrentLotValue, r.NewLotValue, r.SampleId), ct) });

    [HttpDelete("{id:guid}/pairs/{pairId:guid}")]
    public async Task<IActionResult> RemovePair(Guid id, Guid pairId, CancellationToken ct)
    {
        await sender.Send(new RemoveLotPairCommand(id, pairId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateLotComparisonCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> SignOff(Guid id, CancellationToken ct)
    {
        await sender.Send(new SignOffLotComparisonCommand(id), ct);
        return NoContent();
    }
}
