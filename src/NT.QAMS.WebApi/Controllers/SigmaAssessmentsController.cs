using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>Analytical Six-Sigma assessments: σ = (TEa − |bias|) / CV with QC-design guidance.</summary>
[ApiController]
[Route("api/sigma-assessments")]
[Authorize]
public sealed class SigmaAssessmentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetSigmaAssessmentsQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSigmaAssessmentByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateSigmaAssessmentRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateSigmaAssessmentCommand(
            request.Analyte, request.Unit, request.AllowableTotalErrorPct, request.BiasPct, request.CvPct), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateInputs(Guid id, UpdateSigmaInputsRequest request, CancellationToken ct)
    {
        await sender.Send(new UpdateSigmaInputsCommand(
            id, request.AllowableTotalErrorPct, request.BiasPct, request.CvPct), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, CancellationToken ct)
    {
        await sender.Send(new SignOffSigmaAssessmentCommand(id), ct);
        return NoContent();
    }
}
