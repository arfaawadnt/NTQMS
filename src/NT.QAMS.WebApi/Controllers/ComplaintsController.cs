using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Improvement.Commands;
using NT.QAMS.Contracts.Improvement;
using NT.QAMS.Domain.Improvement;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/complaints")]
[Authorize]
public sealed class ComplaintsController(ISender sender) : ControllerBase
{
    /// <summary>QM and TenantAdmin may see confidential reporter identities; other roles get them masked.</summary>
    private bool CanViewConfidential =>
        User.IsInRole("QualityManager") || User.IsInRole("TenantAdmin");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetComplaintsQuery(status, CanViewConfidential), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetComplaintByIdQuery(id, CanViewConfidential), ct));

    [HttpPost]
    public async Task<IActionResult> Log(LogComplaintRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new LogComplaintCommand(
            Enum.Parse<ComplaintChannel>(request.Channel, ignoreCase: true),
            request.ComplainantName, request.ComplainantContact,
            request.Confidential, request.Subject, request.Description,
            request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        await sender.Send(new AcknowledgeComplaintCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/validate")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Validate(Guid id, ValidateComplaintRequest request, CancellationToken ct)
    {
        await sender.Send(new ValidateComplaintCommand(id, request.Justified, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/start-investigation")]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> StartInvestigation(Guid id, CancellationToken ct)
    {
        await sender.Send(new StartComplaintInvestigationCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/outcome")]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> LogOutcome(Guid id, LogComplaintOutcomeRequest request, CancellationToken ct)
    {
        await sender.Send(new LogComplaintOutcomeCommand(id, request.Outcome), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = Roles.QmDeptAdmin)]
    public async Task<IActionResult> Resolve(Guid id, ResolveComplaintRequest request, CancellationToken ct)
    {
        await sender.Send(new ResolveComplaintCommand(id, request.Resolution), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = Roles.QmOrAdmin)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseComplaintCommand(id), ct);
        return NoContent();
    }
}
