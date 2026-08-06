using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.RiskGovernance;
using NT.QAMS.Application.SupplierQuality;
using NT.QAMS.Contracts.Governance;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/risks")]
[Authorize]
public sealed class RisksController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetRisksQuery(status, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetRiskByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Assess(AssessRiskRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new AssessRiskCommand(
            request.Title, request.Category, request.Likelihood, request.Impact,
            request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/actions")]
    public async Task<IActionResult> AddMitigation(Guid id, AddMitigationRequest request, CancellationToken ct) =>
        Ok(new { actionId = await sender.Send(new AddMitigationCommand(
            id, request.Description, request.OwnerId, request.DueDate), ct) });

    [HttpPost("{id:guid}/actions/{actionId:guid}/complete")]
    public async Task<IActionResult> CompleteMitigation(Guid id, Guid actionId, CancellationToken ct)
    {
        await sender.Send(new CompleteMitigationCommand(id, actionId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/residual")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Approve)]
    public async Task<IActionResult> RecordResidual(Guid id, ResidualAssessmentRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordResidualCommand(id, request.Likelihood, request.Impact), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.Risks, PermissionAction.Void)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseRiskCommand(id), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/changes")]
[Authorize]
public sealed class ChangeRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetChangesQuery(status, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetChangeByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Propose(ProposeChangeRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ProposeChangeCommand(request.Title, request.ImpactAnalysis,
            request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/risk")]
    public async Task<IActionResult> LinkRisk(Guid id, LinkRiskRequest request, CancellationToken ct)
    {
        await sender.Send(new LinkRiskCommand(id, request.RiskItemId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(PermissionCatalog.ChangeControl, PermissionAction.Sign)]
    public async Task<IActionResult> Approve(Guid id, ApproveChangeRequest request, CancellationToken ct)
    {
        await sender.Send(new ApproveChangeCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [RequirePermission(PermissionCatalog.ChangeControl, PermissionAction.Void)]
    public async Task<IActionResult> Reject(Guid id, RejectChangeRequest request, CancellationToken ct)
    {
        await sender.Send(new RejectChangeCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CloseChangeRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseChangeCommand(id, request.ImplementationNotes), ct);
        return NoContent();
    }

    /// <summary>Post-implementation review: verify the implemented change was effective (F-11).</summary>
    [HttpPost("{id:guid}/review")]
    [RequirePermission(PermissionCatalog.ChangeControl, PermissionAction.Approve)]
    public async Task<IActionResult> Review(Guid id, ReviewChangeRequest request, CancellationToken ct)
    {
        await sender.Send(new ReviewChangeCommand(id, request.Effective, request.Notes), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/management-reviews")]
[Authorize]
public sealed class ManagementReviewsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetReviewsQuery(page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetReviewByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.ManagementReviews, PermissionAction.Create)]
    public async Task<IActionResult> Schedule(ScheduleReviewRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ScheduleReviewCommand(
            request.Title, request.ReviewDate, request.ParticipantUserIds,
            request.Agenda, request.MeetingLink,
            request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/decisions")]
    [RequirePermission(PermissionCatalog.ManagementReviews, PermissionAction.Edit)]
    public async Task<IActionResult> AddDecision(Guid id, AddDecisionRequest request, CancellationToken ct) =>
        Ok(new { decisionId = await sender.Send(new AddDecisionCommand(
            id, request.Description, request.OwnerId, request.DueDate), ct) });

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCatalog.ManagementReviews, PermissionAction.Sign)]
    public async Task<IActionResult> Close(Guid id, CloseReviewRequest request, CancellationToken ct)
    {
        await sender.Send(new CloseReviewCommand(id, request.Minutes, request.Password, request.Pin), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetSuppliersQuery(status, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSupplierByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Register(RegisterSupplierRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new RegisterSupplierCommand(request.Name, request.SupplierType,
            request.BranchId, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/certificates")]
    public async Task<IActionResult> AddCertificate(Guid id, AddCertificateRequest request, CancellationToken ct) =>
        Ok(new { certificateId = await sender.Send(new AddCertificateCommand(
            id, request.CertificateType, request.ExpiresAt, request.FileId), ct) });

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(PermissionCatalog.Suppliers, PermissionAction.Sign)]
    public async Task<IActionResult> Approve(Guid id, ApproveSupplierRequest request, CancellationToken ct)
    {
        await sender.Send(new ApproveSupplierCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    [RequirePermission(PermissionCatalog.Suppliers, PermissionAction.Void)]
    public async Task<IActionResult> Suspend(Guid id, SuspendSupplierRequest request, CancellationToken ct)
    {
        await sender.Send(new SuspendSupplierCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/evaluations")]
    public async Task<IActionResult> Evaluations(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetEvaluationsQuery(id), ct));

    [HttpPost("{id:guid}/evaluations")]
    [RequirePermission(PermissionCatalog.Suppliers, PermissionAction.Approve)]
    public async Task<IActionResult> RecordEvaluation(
        Guid id, RecordEvaluationRequest request, CancellationToken ct) =>
        Ok(new { evaluationId = await sender.Send(new RecordEvaluationCommand(
            id, request.PeriodStart, request.PeriodEnd,
            request.Criteria.Select(c => (c.Criterion, c.Weight, c.Score)).ToList()), ct) });
}
