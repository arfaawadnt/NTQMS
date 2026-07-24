using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Reporting;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Reporting read side: live KPIs, real snapshot-backed history, NC Pareto and
/// SLA compliance. Read models only — never a source of truth, and never
/// fabricated: every figure is computed from operational rows or the snapshot
/// table, each response carrying its freshness stamp.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet("kpis")]
    public async Task<IActionResult> Kpis(CancellationToken ct) =>
        Ok(await sender.Send(new GetDashboardKpisQuery(), ct));

    [HttpGet("kpi-history")]
    public async Task<IActionResult> KpiHistory([FromQuery] int days = 90, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetKpiHistoryQuery(days), ct));

    [HttpGet("nc-pareto")]
    public async Task<IActionResult> NcPareto(CancellationToken ct) =>
        Ok(await sender.Send(new GetNcParetoQuery(), ct));

    [HttpGet("sla-compliance")]
    public async Task<IActionResult> SlaCompliance(CancellationToken ct) =>
        Ok(await sender.Send(new GetSlaComplianceQuery(), ct));
}
