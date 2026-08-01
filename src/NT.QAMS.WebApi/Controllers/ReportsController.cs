using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Reporting;
using NT.QAMS.Contracts.Reporting;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Reporting read side: live KPIs, real snapshot-backed history, NC Pareto, SLA
/// compliance, and the composite quality analytics behind the Quality Statistics
/// and ISO/IEC 17025 §8.9.2 management-review views. Read models only — never a
/// source of truth, and never fabricated: every figure is computed from
/// operational rows or the snapshot table, each response carrying its freshness
/// stamp.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet("kpis")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.View)]
    public async Task<IActionResult> Kpis(CancellationToken ct) =>
        Ok(await sender.Send(new GetDashboardKpisQuery(), ct));

    [HttpGet("kpi-history")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.View)]
    public async Task<IActionResult> KpiHistory([FromQuery] int days = 90, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetKpiHistoryQuery(days), ct));

    [HttpGet("nc-pareto")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.View)]
    public async Task<IActionResult> NcPareto(CancellationToken ct) =>
        Ok(await sender.Send(new GetNcParetoQuery(), ct));

    [HttpGet("sla-compliance")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.View)]
    public async Task<IActionResult> SlaCompliance(CancellationToken ct) =>
        Ok(await sender.Send(new GetSlaComplianceQuery(), ct));

    /// <summary>
    /// Every quality-analytics section the caller is entitled to see, optionally
    /// narrowed to a branch and/or department. Sections the caller cannot view are
    /// omitted from the payload rather than filtered in the browser.
    /// </summary>
    [HttpGet("quality-analytics")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.View)]
    public async Task<IActionResult> QualityAnalytics(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? departmentId,
        CancellationToken ct) =>
        Ok(await sender.Send(new GetQualityAnalyticsQuery(branchId, departmentId), ct));

    /// <summary>The tenant's Quality Health Score weighting.</summary>
    [HttpGet("quality-health-profile")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.View)]
    public async Task<IActionResult> QualityHealthProfile(CancellationToken ct) =>
        Ok(await sender.Send(new GetQualityHealthProfileQuery(), ct));

    /// <summary>
    /// Redefines how the composite score is calculated. A controlled change: it
    /// requires <c>reports.manage</c> and a reason, and lands in the audit trail.
    /// </summary>
    [HttpPut("quality-health-profile")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.Manage)]
    public async Task<IActionResult> UpdateQualityHealthProfile(
        UpdateQualityHealthWeightsRequest request, CancellationToken ct)
    {
        await sender.Send(new UpdateQualityHealthWeightsCommand(request.Weights, request.Reason), ct);
        return NoContent();
    }
}
