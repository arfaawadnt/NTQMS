using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Accreditation.Commands;
using NT.QAMS.Application.Accreditation.Queries;
using NT.QAMS.Contracts.Accreditation;
using NT.QAMS.Domain.Accreditation;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Accreditation &amp; Standards Compliance API (HQMS M07). Maintains standard sets
/// (GAHAR/JCI/ISO) and their measurable elements, captures self-assessment, links any
/// record as evidence, and serves the live readiness and gap-analysis views — so
/// compliance status is a measured figure rather than an assumption.
/// </summary>
[ApiController]
[Route("api/standards")]
[Authorize]
public sealed class StandardsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetStandardSetsQuery(status), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetStandardSetByIdQuery(id), ct));

    /// <summary>Overall and per-chapter readiness (weighted compliance %).</summary>
    [HttpGet("{id:guid}/readiness")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.View)]
    public async Task<IActionResult> Readiness(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetReadinessDashboardQuery(id), ct));

    /// <summary>Prioritised list of elements needing attention (no evidence, unassessed, or non-compliant).</summary>
    [HttpGet("{id:guid}/gap-analysis")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.View)]
    public async Task<IActionResult> GapAnalysis(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetGapAnalysisQuery(id), ct));

    [HttpGet("elements/{elementId:guid}/evidence")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.View)]
    public async Task<IActionResult> ElementEvidence(Guid elementId, CancellationToken ct) =>
        Ok(await sender.Send(new GetElementEvidenceQuery(elementId), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.Create)]
    public async Task<IActionResult> Define(DefineStandardSetRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new DefineStandardSetCommand(
            RequestEnum.Parse<AccreditationFramework>(request.Framework),
            request.Name, request.Version), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/elements")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.Create)]
    public async Task<IActionResult> AddElement(Guid id, AddStandardElementRequest request, CancellationToken ct)
    {
        var elementId = await sender.Send(new AddStandardElementCommand(
            id, request.ChapterCode, request.ChapterTitle, request.StandardCode,
            request.ElementCode, request.Text, request.Weight), ct);
        return Ok(new { elementId });
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.Approve)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ActivateStandardSetCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.Void)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await sender.Send(new ArchiveStandardSetCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/elements/{elementId:guid}/assess")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.Edit)]
    public async Task<IActionResult> Assess(
        Guid id, Guid elementId, AssessElementRequest request, CancellationToken ct)
    {
        await sender.Send(new AssessElementCommand(
            id, elementId, RequestEnum.Parse<ComplianceStatus>(request.Status), request.Note), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/evidence")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.Edit)]
    public async Task<IActionResult> LinkEvidence(Guid id, LinkEvidenceRequest request, CancellationToken ct)
    {
        var evidenceId = await sender.Send(new LinkEvidenceCommand(
            id, request.ElementId, RequestEnum.Parse<EvidenceSourceType>(request.SourceType),
            request.SourceId, request.SourceRef, request.Description), ct);
        return Ok(new { evidenceId });
    }

    [HttpDelete("evidence/{evidenceId:guid}")]
    [RequirePermission(PermissionCatalog.Standards, PermissionAction.Edit)]
    public async Task<IActionResult> UnlinkEvidence(Guid evidenceId, CancellationToken ct)
    {
        await sender.Send(new UnlinkEvidenceCommand(evidenceId), ct);
        return NoContent();
    }
}
