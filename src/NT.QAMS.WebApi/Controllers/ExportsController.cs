using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.ComplianceLedger;
using NT.QAMS.Application.Improvement.Commands;
using NT.QAMS.Application.Improvement.Queries;
using NT.QAMS.Application.Reporting;
using NT.QAMS.Application.RiskGovernance;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Part 11 §11.10(b) "accurate and complete copies": register and ledger
/// exports (real XLSX) and the management review pack (paginated PDF), all
/// stamped with tenant/actor/instant provenance. Every export is itself
/// recorded as a security event (who exported what, when).
/// </summary>
[ApiController]
[Route("api/exports")]
[Authorize]
public sealed class ExportsController(
    ISender sender, IExportService exports, IAppDbContext db,
    ICurrentTenant tenant, ICurrentUser user, ISecurityEventLog security, IClock clock)
    : ControllerBase
{
    [HttpGet("nonconformances.xlsx")]
    public async Task<IActionResult> NcRegister(CancellationToken ct)
    {
        var items = await sender.Send(new GetNcsQuery(null), ct);
        var pack = await PackAsync("Nonconformance Register", ct,
            new ExportTable(
                "Nonconformances",
                ["Reference", "Title", "Status", "Severity", "RPN", "Source", "Created (UTC)"],
                items.Select(n => (IReadOnlyList<string>)
                [
                    n.NcRef, n.Title, n.Status, n.Severity.ToString(), n.Rpn.ToString(),
                    n.SourceType, n.CreatedAtUtc.ToString("u"),
                ]).ToList()));

        await LogExportAsync("nonconformances.xlsx", ct);
        return File(exports.ToXlsx(pack),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"nc-register-{clock.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("audit-trail.xlsx")]
    [Authorize(Roles = "QualityManager,TenantAdmin,ExternalAuditor")]
    public async Task<IActionResult> AuditTrail([FromQuery] int take = 1000, CancellationToken ct = default)
    {
        var entries = await sender.Send(new GetAuditTrailQuery(null, take), ct);
        var changes = await sender.Send(new GetFieldChangesQuery(null, take), ct);
        var pack = await PackAsync("Compliance Audit Trail", ct,
            new ExportTable(
                "Event Trail",
                ["Seq", "Occurred (UTC)", "Event", "Payload", "Entry Hash"],
                entries.Select(e => (IReadOnlyList<string>)
                [
                    e.Sequence.ToString(), e.OccurredAtUtc.ToString("u"), e.EventType, e.Payload, e.EntryHash,
                ]).ToList()),
            new ExportTable(
                "Field-Level Changes",
                ["Occurred (UTC)", "Entity", "Record", "Action", "Field", "From", "To", "Actor"],
                changes.Select(f => (IReadOnlyList<string>)
                [
                    f.OccurredAtUtc.ToString("u"), f.EntityType, f.EntityId, f.Action,
                    f.Property ?? "", f.OldValue ?? "", f.NewValue ?? "", f.Actor,
                ]).ToList()));

        await LogExportAsync("audit-trail.xlsx", ct);
        return File(exports.ToXlsx(pack),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"audit-trail-{clock.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("review-pack/{reviewId:guid}.pdf")]
    [Authorize(Roles = "QualityManager,TenantAdmin")]
    public async Task<IActionResult> ReviewPack(Guid reviewId, CancellationToken ct)
    {
        var review = await sender.Send(new GetReviewByIdQuery(reviewId), ct);
        var kpis = await sender.Send(new GetDashboardKpisQuery(), ct);
        var pareto = await sender.Send(new GetNcParetoQuery(), ct);

        var pack = await PackAsync($"Management Review Pack — {review.ReviewRef}", ct,
            new ExportTable(
                "Review",
                ["Reference", "Title", "Date", "Status", "Participants"],
                [[review.ReviewRef, review.Title, review.ReviewDate.ToString("yyyy-MM-dd"), review.Status, review.Participants]]),
            new ExportTable(
                "Quality KPIs (live at generation)",
                ["Open NCs", "Overdue CAPA", "Open Complaints", "Audits In Progress",
                 "Equipment OOS", "High Residual Risks", "Overdue Tasks", "Unsatisfactory PT"],
                [[kpis.OpenNcs.ToString(), kpis.OverdueCapaActions.ToString(), kpis.OpenComplaints.ToString(),
                  kpis.AuditsInProgress.ToString(), kpis.EquipmentOutOfService.ToString(),
                  kpis.HighResidualRisks.ToString(), kpis.OverdueTasks.ToString(), kpis.PtUnsatisfactory.ToString()]]),
            new ExportTable(
                "Nonconformances by Source (Pareto)",
                ["Source", "Count"],
                pareto.Select(b => (IReadOnlyList<string>)[b.SourceType, b.Count.ToString()]).ToList()),
            new ExportTable(
                "Decisions",
                ["Description", "Owner", "Due"],
                review.Decisions.Select(d => (IReadOnlyList<string>)
                    [d.Description, d.OwnerId.ToString(), d.DueDate.ToString("yyyy-MM-dd")]).ToList()));

        await LogExportAsync($"review-pack/{review.ReviewRef}.pdf", ct);
        return File(exports.ToPdf(pack), "application/pdf",
            $"review-pack-{review.ReviewRef}-{clock.UtcNow:yyyyMMdd}.pdf");
    }

    private async Task<ExportPack> PackAsync(string title, CancellationToken ct, params ExportTable[] tables)
    {
        var tenantName = tenant.TenantId is { } id
            ? (await db.Tenants.FindAsync([id], ct))?.Name ?? "(unknown)"
            : "(platform)";
        return new ExportPack(title, tenantName, user.DisplayName ?? "system", clock.UtcNow, tables);
    }

    private Task LogExportAsync(string what, CancellationToken ct) =>
        security.WriteAsync("RECORD_EXPORTED", tenant.TenantId, user.DisplayName, what, ct);
}
