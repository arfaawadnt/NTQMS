using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [FromQuery] string? status, [FromQuery] string? search, CancellationToken ct) =>
        Ok(await sender.Send(new GetDocumentsQuery(status, search), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetDocumentByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateDocumentRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateDocumentCommand(
            request.Code, request.Title, request.Category, request.FileId, request.ChangeSummary), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await sender.Send(new SubmitDocumentForReviewCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/recommend")]
    [Authorize(Roles = "DepartmentHead,QualityManager,TenantAdmin")]
    public async Task<IActionResult> Recommend(Guid id, CancellationToken ct)
    {
        await sender.Send(new RecommendDocumentCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "DepartmentHead,QualityManager,TenantAdmin")]
    public async Task<IActionResult> Reject(Guid id, RejectVersionRequest request, CancellationToken ct)
    {
        await sender.Send(new RejectDocumentVersionCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> Publish(Guid id, PublishDocumentRequest request, CancellationToken ct)
    {
        await sender.Send(new PublishDocumentCommand(id, request.Pin), ct);
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
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireDocumentCommand(id), ct);
        return NoContent();
    }
}
