using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.MortalityReview;
using NT.QAMS.Contracts.MortalityReview;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.MortalityReview;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Mortality, Morbidity &amp; Peer Review API (HQMS M10): mortality reviews with peer classification
/// and a mandatory independent second review, the complication (morbidity) register, and the
/// mortality rate per 1,000 patient-days from the M24 ADT denominator.
/// </summary>
[ApiController]
[Route("api/mortality-review")]
[Authorize]
public sealed class MortalityReviewController(ISender sender) : ControllerBase
{
    // ── Mortality reviews ──────────────────────────────────────────────────────
    [HttpGet("reviews")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.View)]
    public async Task<IActionResult> ListReviews([FromQuery] string? classification, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetMortalityReviewsQuery(classification, status), ct));

    [HttpGet("reviews/{id:guid}")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.View)]
    public async Task<IActionResult> GetReview(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMortalityByIdQuery(id), ct));

    [HttpGet("rates")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.View)]
    public async Task<IActionResult> Rates([FromQuery] int windowDays = 30, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetMortalityRatesQuery(windowDays), ct));

    [HttpPost("reviews")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Create)]
    public async Task<IActionResult> ReportReview(ReportMortalityRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ReportMortalityCommand(
            request.PatientRef, request.Unit, request.DeathDateUtc, request.PrimaryDiagnosis, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetReview), new { id }, new { id });
    }

    [HttpPost("reviews/{id:guid}/classify")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Edit)]
    public async Task<IActionResult> Classify(Guid id, ClassifyMortalityRequest request, CancellationToken ct)
    {
        await sender.Send(new ClassifyMortalityCommand(
            id, RequestEnum.Parse<DeathClassification>(request.Classification), request.Findings), ct);
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/second-review")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Approve)]
    public async Task<IActionResult> SecondReview(Guid id, SecondReviewRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordSecondReviewCommand(id, request.Notes, request.Concurs), ct);
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/committee-discussed")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Edit)]
    public async Task<IActionResult> CommitteeDiscussed(Guid id, CommitteeDiscussedRequest request, CancellationToken ct)
    {
        await sender.Send(new MarkCommitteeDiscussedCommand(id, request.Learnings), ct);
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/close")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Void)]
    public async Task<IActionResult> CloseReview(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseMortalityCommand(id), ct);
        return NoContent();
    }

    // ── Complication register (morbidity) ──────────────────────────────────────
    [HttpGet("complications")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.View)]
    public async Task<IActionResult> ListComplications([FromQuery] string? type, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetComplicationsQuery(type, status), ct));

    [HttpGet("complications/{id:guid}")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.View)]
    public async Task<IActionResult> GetComplication(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetComplicationByIdQuery(id), ct));

    [HttpPost("complications")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Create)]
    public async Task<IActionResult> ReportComplication(ReportComplicationRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ReportComplicationCommand(
            request.PatientRef, request.Unit,
            RequestEnum.Parse<ComplicationType>(request.Type),
            RequestEnum.Parse<ComplicationSeverity>(request.Severity),
            request.OccurredDateUtc, request.Description, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetComplication), new { id }, new { id });
    }

    [HttpPost("complications/{id:guid}/review")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Edit)]
    public async Task<IActionResult> ReviewComplication(Guid id, ReviewComplicationRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviewComplicationCommand(id, request.Notes, request.Preventable), ct);
        return NoContent();
    }

    [HttpPost("complications/{id:guid}/close")]
    [RequirePermission(PermissionCatalog.MortalityReview, PermissionAction.Void)]
    public async Task<IActionResult> CloseComplication(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseComplicationCommand(id), ct);
        return NoContent();
    }
}
