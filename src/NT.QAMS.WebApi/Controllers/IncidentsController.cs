using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.IncidentReporting.Commands;
using NT.QAMS.Application.IncidentReporting.Queries;
using NT.QAMS.Contracts.IncidentReporting;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Incident &amp; Occurrence Reporting API (HQMS M02). Transitions are verbs-as-subresources
/// mapping 1:1 to commands. Reporting is open to any internal actor (a safety-culture
/// requirement); advancing, closing and sentinel declaration are permission-gated, and the
/// two signing ceremonies (close, declare-sentinel) require an e-signature envelope.
/// </summary>
[ApiController]
[Route("api/incidents")]
[Authorize]
public sealed class IncidentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? category,
        [FromQuery] bool sentinelOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetIncidentsQuery(status, search, category, sentinelOnly, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetIncidentByIdQuery(id), ct));

    /// <summary>Part 11 §11.50 signature manifest for this incident, visible to any viewer of the record.</summary>
    [HttpGet("{id:guid}/signatures")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.View)]
    public async Task<IActionResult> Signatures(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new NT.QAMS.Application.ComplianceLedger.GetSignaturesForSubjectQuery($"INC:{id:N}"), ct));

    /// <summary>Tracks an anonymous report by its one-time follow-up reference (status only).</summary>
    [HttpGet("track")]
    public async Task<IActionResult> Track([FromQuery] string reference, CancellationToken ct) =>
        Ok(await sender.Send(new TrackAnonymousIncidentQuery(reference), ct));

    [HttpPost]
    public async Task<IActionResult> Report(ReportIncidentRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ReportIncidentCommand(
            request.Title, request.Description,
            Enum.Parse<IncidentCategory>(request.Category, ignoreCase: true),
            Enum.Parse<HarmGrade>(request.HarmGrade, ignoreCase: true),
            Enum.Parse<IntakeChannel>(request.Channel, ignoreCase: true),
            request.OccurredAtUtc, request.Location, request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>Submits a report with identity suppressed; returns the one-time follow-up reference.</summary>
    [HttpPost("anonymous")]
    public async Task<IActionResult> ReportAnonymous(ReportAnonymousIncidentRequest request, CancellationToken ct)
    {
        var receipt = await sender.Send(new ReportAnonymousIncidentCommand(
            request.Title, request.Description,
            Enum.Parse<IncidentCategory>(request.Category, ignoreCase: true),
            Enum.Parse<HarmGrade>(request.HarmGrade, ignoreCase: true),
            Enum.Parse<IntakeChannel>(request.Channel, ignoreCase: true),
            request.OccurredAtUtc, request.Location, request.BranchId, request.DepartmentId), ct);
        return Ok(receipt);
    }

    [HttpPost("{id:guid}/triage")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Approve)]
    public async Task<IActionResult> Triage(Guid id, TriageIncidentRequest request, CancellationToken ct)
    {
        await sender.Send(new TriageIncidentCommand(
            id, request.AssigneeId,
            Enum.Parse<IncidentCategory>(request.Category, ignoreCase: true)), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Void)]
    public async Task<IActionResult> Reject(Guid id, RejectIncidentRequest request, CancellationToken ct)
    {
        await sender.Send(new RejectIncidentCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/start-investigation")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Approve)]
    public async Task<IActionResult> StartInvestigation(Guid id, StartInvestigationRequest request, CancellationToken ct)
    {
        await sender.Send(new StartIncidentInvestigationCommand(id, request.InvestigatorId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/contributing-factors")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Edit)]
    public async Task<IActionResult> AddContributingFactor(
        Guid id, AddContributingFactorRequest request, CancellationToken ct)
    {
        await sender.Send(new AddContributingFactorCommand(
            id, Enum.Parse<ContributingFactorCategory>(request.Category, ignoreCase: true), request.Description), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/timeline")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Edit)]
    public async Task<IActionResult> AddTimelineEntry(Guid id, AddTimelineEntryRequest request, CancellationToken ct)
    {
        await sender.Send(new AddTimelineEntryCommand(id, request.OccurredAtUtc, request.Note), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/investigation-summary")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Edit)]
    public async Task<IActionResult> RecordInvestigationSummary(
        Guid id, RecordInvestigationSummaryRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordInvestigationSummaryCommand(id, request.Summary), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit-review")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Approve)]
    public async Task<IActionResult> SubmitForReview(Guid id, CancellationToken ct)
    {
        await sender.Send(new SubmitIncidentForReviewCommand(id), ct);
        return NoContent();
    }

    /// <summary>Closes the incident. Part 11 signing ceremony: account password + signature PIN.</summary>
    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Sign)]
    public async Task<IActionResult> Close(Guid id, CloseIncidentRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseIncidentCommand(id, request.ClosureSummary, request.Password, request.Pin), ct);
        return NoContent();
    }

    /// <summary>Declares a sentinel event. Part 11 signing ceremony: account password + signature PIN.</summary>
    [HttpPost("{id:guid}/declare-sentinel")]
    [RequirePermission(PermissionCatalog.Incidents, PermissionAction.Sign)]
    public async Task<IActionResult> DeclareSentinel(Guid id, DeclareSentinelRequest request, CancellationToken ct)
    {
        await sender.Send(new DeclareSentinelCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }

    /// <summary>
    /// Raises a Nonconformance/CAPA from this incident and back-links it ("one loop, many
    /// sources"). Requires the NC create privilege; idempotent — returns the linked CAPA id.
    /// </summary>
    [HttpPost("{id:guid}/raise-capa")]
    [RequirePermission(PermissionCatalog.Nonconformances, PermissionAction.Create)]
    public async Task<IActionResult> RaiseCapa(Guid id, CancellationToken ct)
    {
        var ncId = await sender.Send(new RaiseCapaFromIncidentCommand(id), ct);
        return Ok(new { ncId });
    }
}
