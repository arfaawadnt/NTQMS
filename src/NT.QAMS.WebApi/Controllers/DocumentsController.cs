using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.DocumentControl;
using NT.QAMS.Application.DocumentControl.Commands;
using NT.QAMS.Application.DocumentControl.Queries;
using NT.QAMS.Contracts.DocumentControl;
using NT.QAMS.Domain.DocumentControl;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Document Control workflow. Recommend = department-head review; publish = QM
/// approval (role gates per the seed matrix; SoD is enforced in the aggregate
/// regardless of role).
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetDocumentsQuery(status, search, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetDocumentByIdQuery(id), ct));

    /// <summary>Records the completed periodic review and re-arms the cycle (ISO 17025 §8.3).</summary>
    [HttpPost("{id:guid}/confirm-review")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.Sign)]
    public async Task<IActionResult> ConfirmReview(Guid id, CancellationToken ct)
    {
        await sender.Send(new ConfirmDocumentReviewCommand(id), ct);
        return NoContent();
    }

    /// <summary>Part 11 §11.50 signature manifest for this document, visible to any viewer of the record.</summary>
    [HttpGet("{id:guid}/signatures")]
    public async Task<IActionResult> Signatures(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new NT.QAMS.Application.ComplianceLedger.GetSignaturesForSubjectQuery($"DOC:{id:N}"), ct));

    /// <summary>The current user confirms they have read and understood the published version.</summary>
    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new AcknowledgeDocumentCommand(id), ct) });

    /// <summary>Whether the current user has acknowledged the current published version.</summary>
    [HttpGet("{id:guid}/my-acknowledgement")]
    public async Task<IActionResult> MyAcknowledgement(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMyDocumentAcknowledgementQuery(id), ct));

    /// <summary>Read-and-understand coverage for this document (quality-management view).</summary>
    [HttpGet("{id:guid}/acknowledgements")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.View)]
    public async Task<IActionResult> Acknowledgements(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetDocumentAcknowledgementsQuery(id), ct));

    /// <summary>Controlled printed-copy / distribution register for this document (ISO 17025 §8.3).</summary>
    [HttpGet("{id:guid}/controlled-copies")]
    public async Task<IActionResult> ControlledCopies(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetControlledCopiesQuery(id), ct));

    [HttpPost("{id:guid}/controlled-copies")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.Edit)]
    public async Task<IActionResult> IssueControlledCopy(Guid id, IssueControlledCopyRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new IssueControlledCopyCommand(id, request.Holder), ct) });

    [HttpPost("controlled-copies/{copyId:guid}/close")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.Edit)]
    public async Task<IActionResult> CloseControlledCopy(Guid copyId, CloseControlledCopyRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseControlledCopyCommand(copyId, request.Outcome), ct);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDocumentRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateDocumentCommand(
            request.Code, request.Title, request.Category, request.FileId, request.ChangeSummary, request.ReviewCycleMonths), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await sender.Send(new SubmitDocumentForReviewCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/recommend")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.Approve)]
    public async Task<IActionResult> Recommend(Guid id, CancellationToken ct)
    {
        await sender.Send(new RecommendDocumentCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.Approve)]
    public async Task<IActionResult> Reject(Guid id, RejectVersionRequest request, CancellationToken ct)
    {
        await sender.Send(new RejectDocumentVersionCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.Sign)]
    // SEC-013: password+PIN signing ceremony — throttled per actor so a PIN
    // cannot be brute-forced inside a valid session.
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Security.RateLimiting.ESignaturePolicy)]
    public async Task<IActionResult> Publish(Guid id, PublishDocumentRequest request, CancellationToken ct)
    {
        await sender.Send(new PublishDocumentCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> DraftNewVersion(Guid id, DraftNewVersionRequest request, CancellationToken ct)
    {
        await sender.Send(new DraftNewVersionCommand(
            id, request.FileId, request.ChangeSummary,
            Enum.Parse<VersionBump>(request.Bump, ignoreCase: true)), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/retire")]
    [RequirePermission(PermissionCatalog.Documents, PermissionAction.Void)]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireDocumentCommand(id), ct);
        return NoContent();
    }
}
