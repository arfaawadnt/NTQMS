using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Instrument-to-instrument comparability studies.</summary>
[ApiController]
[Route("api/instrument-comparabilities")]
[Authorize]
public sealed class InstrumentComparabilitiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetInstrumentComparabilitiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetInstrumentComparabilityByIdQuery(id), ct));

    /// <summary>Part 11 §11.50 signature manifest for this study, visible to any viewer of the record.</summary>
    [HttpGet("{id:guid}/signatures")]
    public async Task<IActionResult> Signatures(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new NT.QAMS.Application.ComplianceLedger.GetSignaturesForSubjectQuery($"ICP:{id:N}"), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateInstrumentComparabilityRequest r, CancellationToken ct)
    {
        var id = await sender.Send(new CreateInstrumentComparabilityCommand(r.Analyte, r.Unit, r.ReferenceInstrument, r.AllowableBiasPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/readings")]
    public async Task<IActionResult> AddReading(Guid id, AddInstrumentReadingRequest r, CancellationToken ct) =>
        Ok(new { readingId = await sender.Send(new AddInstrumentReadingCommand(id, r.Instrument, r.SampleId, r.Value), ct) });

    [HttpDelete("{id:guid}/readings/{readingId:guid}")]
    public async Task<IActionResult> RemoveReading(Guid id, Guid readingId, CancellationToken ct)
    {
        await sender.Send(new RemoveInstrumentReadingCommand(id, readingId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateInstrumentComparabilityCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new SignOffInstrumentComparabilityCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}
