using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Reference-interval verification / transference (CLSI EP28).</summary>
[ApiController]
[Route("api/reference-interval-studies")]
[Authorize]
public sealed class ReferenceIntervalStudiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetReferenceIntervalStudiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetReferenceIntervalStudyByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateReferenceIntervalStudyRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateReferenceIntervalStudyCommand(
            request.Analyte, request.Unit, request.Population, request.Source,
            request.ClaimedLower, request.ClaimedUpper), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/samples")]
    public async Task<IActionResult> AddSample(Guid id, AddReferenceSampleRequest request, CancellationToken ct) =>
        Ok(new
        {
            sampleId = await sender.Send(new AddReferenceSampleCommand(id, request.Value, request.SubjectRef), ct),
        });

    [HttpDelete("{id:guid}/samples/{sampleId:guid}")]
    public async Task<IActionResult> RemoveSample(Guid id, Guid sampleId, CancellationToken ct)
    {
        await sender.Send(new RemoveReferenceSampleCommand(id, sampleId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateReferenceIntervalCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new SignOffReferenceIntervalCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}
