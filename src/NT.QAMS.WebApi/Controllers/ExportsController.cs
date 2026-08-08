using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    /// <summary>Hard ceilings for the generic page export — a formatter, not a bulk-data channel.</summary>
    private const int MaxExportRows = 10_000;
    private const int MaxExportColumns = 40;
    private const int MaxExportStats = 16;
    private const int MaxManualGroups = 40;
    private const int MaxManualTopics = 500;

    /// <summary>
    /// Renders the caller's current register view as a branded document. The
    /// payload is the caller's own filtered view — data they already fetched
    /// under their permissions — so no extra permission gate applies beyond
    /// authentication; the server formats and stamps, it does not re-query.
    /// </summary>
    [HttpPost("page.pdf")]
    public async Task<IActionResult> PagePdf(Contracts.Common.PageExportRequest request, CancellationToken ct)
    {
        var pack = await BuildPagePackAsync(request, ct);
        await LogExportAsync($"page/{request.Title}.pdf", ct);
        return File(exports.ToPagePdf(pack), "application/pdf",
            $"{FileSlug(request.Title)}-{clock.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    /// <summary>
    /// Renders the complete User Manual as a professional PDF. The manual content
    /// lives only in the SPA (the trilingual help catalogue), so the caller posts
    /// it already localized; the server lays it out and stamps provenance — the
    /// same "format the caller's own view" contract as the generic page export, so
    /// no permission gate applies beyond authentication.
    /// </summary>
    [HttpPost("manual.pdf")]
    public async Task<IActionResult> ManualPdf(Contracts.Common.ManualExportRequest request, CancellationToken ct)
    {
        if (request.Groups.Count is 0 or > MaxManualGroups
            || request.Groups.Sum(g => g.Topics.Count) is 0 or > MaxManualTopics)
        {
            throw new NT.QAMS.SharedKernel.Primitives.DomainException(
                "EXPORT-003", "The manual payload is empty or exceeds the size ceiling.");
        }

        var tenantName = tenant.TenantId is { } id
            ? (await db.Tenants.FindAsync([id], ct))?.Name ?? "(unknown)"
            : "(platform)";
        var language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim();
        var pack = new ManualExportPack(
            tenantName, user.DisplayName ?? "system", clock.UtcNow, language, request.Groups);

        await LogExportAsync("manual.pdf", ct);
        return File(exports.ToManualPdf(pack), "application/pdf",
            $"nt-qams-user-manual-{language}-{clock.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    /// <summary>Same view rendered as a real workbook (frozen, filterable grid).</summary>
    [HttpPost("page.xlsx")]
    public async Task<IActionResult> PageXlsx(Contracts.Common.PageExportRequest request, CancellationToken ct)
    {
        var pack = await BuildPagePackAsync(request, ct);
        await LogExportAsync($"page/{request.Title}.xlsx", ct);
        return File(exports.ToPageXlsx(pack),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{FileSlug(request.Title)}-{clock.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    private async Task<PageExportPack> BuildPagePackAsync(
        Contracts.Common.PageExportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new NT.QAMS.SharedKernel.Primitives.DomainException(
                "EXPORT-001", "An export title is required.");
        }

        if (request.Columns.Count is 0 or > MaxExportColumns
            || request.Rows.Count > MaxExportRows
            || request.Stats.Count > MaxExportStats
            || request.Rows.Any(r => r.Count != request.Columns.Count))
        {
            throw new NT.QAMS.SharedKernel.Primitives.DomainException(
                "EXPORT-002", "The export payload is malformed or exceeds the size ceiling.");
        }

        var tenantName = tenant.TenantId is { } id
            ? (await db.Tenants.FindAsync([id], ct))?.Name ?? "(unknown)"
            : "(platform)";
        return new PageExportPack(
            request.Title.Trim(),
            tenantName,
            user.DisplayName ?? "system",
            clock.UtcNow,
            string.IsNullOrWhiteSpace(request.FiltersSummary) ? null : request.FiltersSummary.Trim(),
            request.Stats.Select(s => new ExportStat(s.Label, s.Value, s.Tone)).ToList(),
            new ExportTable(request.Title.Trim(), request.Columns, request.Rows));
    }

    /// <summary>A filesystem-safe slug from the localized page title.</summary>
    private static string FileSlug(string title)
    {
        var cleaned = new string(title.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray());
        while (cleaned.Contains("--")) { cleaned = cleaned.Replace("--", "-"); }
        cleaned = cleaned.Trim('-');
        return cleaned.Length == 0 ? "export" : cleaned.Length > 60 ? cleaned[..60] : cleaned;
    }

    [HttpGet("nonconformances.xlsx")]
    public async Task<IActionResult> NcRegister(CancellationToken ct)
    {
        // Part 11 §11.10(b): a register export must be COMPLETE — walk every
        // page of the API-004 envelope rather than truncating at one page.
        var items = new List<Contracts.Improvement.NcListItemDto>();
        Contracts.Common.PagedResponse<Contracts.Improvement.NcListItemDto> page;
        var pageNumber = 1;
        do
        {
            page = await sender.Send(new GetNcsQuery(null, Page: pageNumber++, PageSize: PageRequest.MaxPageSize), ct);
            items.AddRange(page.Items);
        }
        while (page.HasMore);
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
    [RequirePermission(PermissionCatalog.Compliance, PermissionAction.Export)]
    public async Task<IActionResult> AuditTrail([FromQuery] int take = 1000, CancellationToken ct = default)
    {
        var entries = await sender.Send(new GetAuditTrailQuery(null, take), ct);
        var changes = await sender.Send(new GetFieldChangesQuery(null, take), ct);

        // F-13: the export carries a live chain-integrity attestation, so the copy
        // itself is evidence that the trail was intact at the moment it was drawn.
        var integrity = tenant.TenantId is { } tid
            ? await sender.Send(new VerifyChainQuery(tid), ct)
            : new ChainVerificationDto(false, 0, null);

        var pack = await PackAsync("Compliance Audit Trail", ct,
            new ExportTable(
                "Integrity Attestation",
                ["Chain integrity", "Entries verified", "First break at sequence", "Entries in this export"],
                [[
                    integrity.Ok ? "OK — chain intact" : "BROKEN",
                    integrity.VerifiedEntries.ToString(),
                    integrity.BrokenAtSequence?.ToString() ?? "—",
                    entries.Count.ToString(),
                ]]),
            new ExportTable(
                "Event Trail",
                ["Seq", "Occurred (UTC)", "Event", "Payload", "Entry Hash"],
                entries.Select(e => (IReadOnlyList<string>)
                [
                    e.Sequence.ToString(), e.OccurredAtUtc.ToString("u"), e.EventType, e.Payload, e.EntryHash,
                ]).ToList()),
            new ExportTable(
                "Field-Level Changes",
                ["Occurred (UTC)", "Entity", "Record", "Action", "Field", "From", "To", "Actor", "Reason"],
                changes.Select(f => (IReadOnlyList<string>)
                [
                    f.OccurredAtUtc.ToString("u"), f.EntityType, f.EntityId, f.Action,
                    f.Property ?? "", f.OldValue ?? "", f.NewValue ?? "", f.Actor, f.Reason ?? "",
                ]).ToList()));

        await LogExportAsync("audit-trail.xlsx", ct);
        return File(exports.ToXlsx(pack),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"audit-trail-{clock.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("signatures.xlsx")]
    [RequirePermission(PermissionCatalog.Compliance, PermissionAction.Export)]
    public async Task<IActionResult> SignatureManifest([FromQuery] int take = 1000, CancellationToken ct = default)
    {
        var signatures = await sender.Send(new GetSignatureLogQuery(take), ct);
        var pack = await PackAsync("Electronic Signature Manifest", ct,
            new ExportTable(
                "Signatures",
                ["Signed (UTC)", "Signer", "Meaning", "Subject", "Content Hash"],
                signatures.Select(s => (IReadOnlyList<string>)
                [
                    s.SignedAtUtc.ToString("u"), s.SignerDisplay, s.Meaning, s.SubjectRef, s.ContentHash,
                ]).ToList()));

        await LogExportAsync("signatures.xlsx", ct);
        return File(exports.ToXlsx(pack),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"signature-manifest-{clock.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("review-pack/{reviewId:guid}.pdf")]
    [RequirePermission(PermissionCatalog.ManagementReviews, PermissionAction.Export)]
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

    /// <summary>
    /// The comprehensive Quality Analytics report as a branded PDF (score gauge,
    /// weighted-component progress bars, Pareto bars, risk heat-matrix). Re-queries
    /// the same computation the dashboard shows, honouring the caller's branch/
    /// department scope and view permissions (a section the caller cannot see is
    /// absent from both the analytics and the report).
    /// </summary>
    [HttpGet("quality-analytics.pdf")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.Export)]
    public async Task<IActionResult> QualityAnalyticsPdf(
        [FromQuery] Guid? branchId, [FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var pack = await BuildAnalyticsPackAsync(branchId, departmentId, ct);
        await LogExportAsync("quality-analytics.pdf", ct);
        return File(exports.ToQualityAnalyticsReportPdf(pack), "application/pdf",
            $"quality-analytics-{clock.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    /// <summary>The same report as a real workbook — a health-score summary sheet plus one sheet per sub-system.</summary>
    [HttpGet("quality-analytics.xlsx")]
    [RequirePermission(PermissionCatalog.Reports, PermissionAction.Export)]
    public async Task<IActionResult> QualityAnalyticsXlsx(
        [FromQuery] Guid? branchId, [FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var pack = await BuildAnalyticsPackAsync(branchId, departmentId, ct);
        await LogExportAsync("quality-analytics.xlsx", ct);
        return File(exports.ToQualityAnalyticsReportXlsx(pack),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"quality-analytics-{clock.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    private async Task<QualityAnalyticsReportPack> BuildAnalyticsPackAsync(
        Guid? branchId, Guid? departmentId, CancellationToken ct)
    {
        var analytics = await sender.Send(new GetQualityAnalyticsQuery(branchId, departmentId), ct);

        var tenantName = tenant.TenantId is { } id
            ? (await db.Tenants.FindAsync([id], ct))?.Name ?? "(unknown)"
            : "(platform)";

        // Human-readable scope line — resolved names, so the copy states what it was filtered to.
        var parts = new List<string>();
        if (branchId is { } b)
        {
            var name = await db.Branches.AsNoTracking().Where(x => x.Id == b).Select(x => x.Name).FirstOrDefaultAsync(ct);
            parts.Add($"Branch: {name ?? b.ToString()}");
        }
        if (departmentId is { } d)
        {
            var name = await db.Departments.AsNoTracking().Where(x => x.Id == d).Select(x => x.Name).FirstOrDefaultAsync(ct);
            parts.Add($"Department: {name ?? d.ToString()}");
        }
        var filters = parts.Count == 0 ? null : string.Join(" · ", parts);

        return new QualityAnalyticsReportPack(
            tenantName, user.DisplayName ?? "system", clock.UtcNow, filters, analytics);
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
