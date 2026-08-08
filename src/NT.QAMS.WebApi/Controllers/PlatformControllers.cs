using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Notifications;
using NT.QAMS.Application.Organization;
using NT.QAMS.Contracts.Platform;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize]
public sealed class BranchesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Tree(CancellationToken ct) =>
        Ok(await sender.Send(new GetOrgTreeQuery(), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Organization, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateBranchRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new CreateBranchCommand(request.Code, request.Name, request.City), ct) });

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(PermissionCatalog.Organization, PermissionAction.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeactivateOrgUnitCommand(id, IsBranch: true), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/departments")]
[Authorize]
public sealed class DepartmentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, CancellationToken ct) =>
        Ok(await sender.Send(new GetDepartmentsQuery(branchId), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Organization, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateDepartmentRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new CreateDepartmentCommand(
            request.BranchId, request.Code, request.Name), ct) });

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(PermissionCatalog.Organization, PermissionAction.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeactivateOrgUnitCommand(id, IsBranch: false), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/test-catalog")]
[Authorize]
public sealed class TestCatalogController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetTestCatalogQuery(), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Organization, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateTestRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new CreateTestCommand(
            request.TestCode, request.TestName, request.Methodology, request.TurnaroundHours), ct) });
}

[ApiController]
[Route("api/lovs")]
[Authorize]
public sealed class LovsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? category, CancellationToken ct) =>
        Ok(await sender.Send(new GetLovsQuery(category), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Organization, PermissionAction.Edit)]
    public async Task<IActionResult> Upsert(UpsertLovRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new UpsertLovCommand(
            request.Category, request.Code, request.NameEn, request.NameAr,
            request.NameFr, request.SortOrder), ct) });
}

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(
        [FromQuery] bool unreadOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetMyNotificationsQuery(unreadOnly, page, pageSize), ct));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await sender.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }

    [HttpGet("rules")]
    [RequirePermission(PermissionCatalog.Notifications, PermissionAction.Manage)]
    public async Task<IActionResult> Rules(CancellationToken ct) =>
        Ok(await sender.Send(new GetNotificationRulesQuery(), ct));

    [HttpPost("rules")]
    [RequirePermission(PermissionCatalog.Notifications, PermissionAction.Manage)]
    public async Task<IActionResult> UpsertRule(UpsertNotificationRuleRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new UpsertNotificationRuleCommand(
            request.EventKey, request.RecipientRoles, request.EmailEnabled,
            request.SubjectTemplate, request.BodyTemplate), ct) });

    [HttpGet("monitor")]
    [RequirePermission(PermissionCatalog.Notifications, PermissionAction.Manage)]
    public async Task<IActionResult> Monitor(
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(new GetDispatchMonitorQuery(status, page, pageSize), ct));

    /// <summary>The tenant's mail sender identity and branding for the Mail Management page.</summary>
    [HttpGet("mail-settings")]
    [RequirePermission(PermissionCatalog.Notifications, PermissionAction.Manage)]
    public async Task<IActionResult> MailSettings(CancellationToken ct) =>
        Ok(await sender.Send(new GetMailSettingsQuery(), ct));

    /// <summary>Sets the sender identity used for Mail-type notifications (transport credentials stay in server config).</summary>
    [HttpPut("mail-settings")]
    [RequirePermission(PermissionCatalog.Notifications, PermissionAction.Manage)]
    public async Task<IActionResult> UpdateMailSettings(UpdateMailSettingsRequest request, CancellationToken ct)
    {
        await sender.Send(new UpsertMailSettingsCommand(
            request.FromName, request.FromAddress, request.ReplyTo, request.Enabled,
            request.BrandColor, request.FooterNote), ct);
        return NoContent();
    }
}
