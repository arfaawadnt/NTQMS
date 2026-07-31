using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.IdentityAccess.Commands;
using NT.QAMS.Contracts.IdentityAccess;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Periodic user-access review / recertification (21 CFR Part 11 §11.10(d) /
/// EU Annex 11 §12): tenant administrators open a review, examine the account
/// roster and roles, and record the conclusion. Completed reviews are immutable
/// evidence that access was recertified.
/// </summary>
[ApiController]
[Authorize]
[Route("api/access-reviews")]
[RequirePermission(PermissionCatalog.AccessReviews, PermissionAction.View)]
public sealed class AccessReviewsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetAccessReviewsQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Open(CancellationToken ct) =>
        Ok(new { id = await sender.Send(new OpenAccessReviewCommand(), ct) });

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CompleteAccessReviewRequest request, CancellationToken ct)
    {
        await sender.Send(new CompleteAccessReviewCommand(id, request.ChangesRequired, request.Conclusion), ct);
        return NoContent();
    }
}
