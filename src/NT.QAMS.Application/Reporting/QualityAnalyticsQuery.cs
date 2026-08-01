using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Reporting;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Reporting;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.Reporting;

/// <summary>
/// The analytics behind both the Quality Statistics view and the ISO/IEC 17025
/// §8.9.2 management-review view. Optionally narrowed to a branch and/or
/// department.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.Reports, PermissionAction.View)]
public sealed record GetQualityAnalyticsQuery(Guid? BranchId = null, Guid? DepartmentId = null)
    : IQuery<QualityAnalyticsDto>;

/// <summary>
/// Computes every section from live operational rows. Three rules govern this
/// handler, and each exists to stop the page reporting something it cannot
/// support:
///
/// <list type="number">
/// <item>A section the caller cannot view is <b>not computed and not returned</b>.
/// Hiding it client-side would still ship the figures to the browser.</item>
/// <item>An empty population yields <c>null</c>, never zero. "No documents yet"
/// and "no documents current" are different facts, and only one of them is a
/// finding.</item>
/// <item>A branch/department filter is applied only to records that carry that
/// attribution. Sections over unattributed records are returned unnarrowed and
/// named in <see cref="QualityAnalyticsScopeDto.UnscopedSections"/>, so a filtered
/// view never implies a precision it does not have.</item>
/// </list>
/// </summary>
public sealed class GetQualityAnalyticsHandler(IAppDbContext db, IClock clock, IUserPrivileges privileges)
    : IQueryHandler<GetQualityAnalyticsQuery, QualityAnalyticsDto>
{
    /// <summary>Documents, competency and PT records carry no branch or department.</summary>
    private static readonly string[] NotOrganisationallyAttributed =
        ["documentControl", "competency", "proficiencyTesting"];

    /// <summary>The review-due horizon buckets the report presents, in days.</summary>
    private const int Near = 30;
    private const int Mid = 60;
    private const int Far = 90;

    public async Task<QualityAnalyticsDto> Handle(GetQualityAnalyticsQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var branch = query.BranchId;
        var department = query.DepartmentId;

        var hidden = new List<string>();
        bool Visible(string moduleKey, string section)
        {
            if (privileges.Has(PermissionCatalog.Key(moduleKey, PermissionAction.View)))
            {
                return true;
            }

            hidden.Add(section);
            return false;
        }

        var documents = Visible(PermissionCatalog.Documents, "documentControl")
            ? await DocumentsAsync(today, ct) : null;
        var ncCapa = Visible(PermissionCatalog.Nonconformances, "ncCapa")
            ? await NcCapaAsync(today, branch, department, ct) : null;
        var complaints = Visible(PermissionCatalog.Complaints, "complaints")
            ? await ComplaintsAsync(branch, department, ct) : null;
        var audits = Visible(PermissionCatalog.Audits, "audits")
            ? await AuditsAsync(branch, department, ct) : null;
        var equipment = Visible(PermissionCatalog.Equipment, "equipment")
            ? await EquipmentAsync(today, branch, department, ct) : null;
        var competency = Visible(PermissionCatalog.Competencies, "competency")
            ? await CompetencyAsync(today, ct) : null;
        var pt = Visible(PermissionCatalog.ProficiencyTesting, "proficiencyTesting")
            ? await PtAsync(ct) : null;
        var suppliers = Visible(PermissionCatalog.Suppliers, "suppliers")
            ? await SuppliersAsync(branch, department, ct) : null;
        var risk = Visible(PermissionCatalog.Risks, "risk")
            ? await RiskAsync(today, branch, department, ct) : null;

        var health = await HealthAsync(
            documents, ncCapa, complaints, audits, equipment, competency, pt, suppliers, risk, ct);

        var filterApplied = branch is not null || department is not null;
        var scope = new QualityAnalyticsScopeDto(
            branch,
            department,
            filterApplied,
            filterApplied ? NotOrganisationallyAttributed : [],
            hidden);

        return new QualityAnalyticsDto(
            health, documents, ncCapa, complaints, audits, equipment,
            competency, pt, suppliers, risk, scope, now);
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private async Task<DocumentControlStatsDto> DocumentsAsync(DateOnly today, CancellationToken ct)
    {
        var active = db.Documents.Where(d => d.Status == DocumentStatus.Published);

        var total = await active.CountAsync(ct);
        var overdue = await active.CountAsync(d => d.NextReviewDue != null && d.NextReviewDue < today, ct);
        var within30 = await active.CountAsync(
            d => d.NextReviewDue != null && d.NextReviewDue >= today && d.NextReviewDue <= today.AddDays(Near), ct);
        var within60 = await active.CountAsync(
            d => d.NextReviewDue != null && d.NextReviewDue > today.AddDays(Near) && d.NextReviewDue <= today.AddDays(Mid), ct);
        var within90 = await active.CountAsync(
            d => d.NextReviewDue != null && d.NextReviewDue > today.AddDays(Mid) && d.NextReviewDue <= today.AddDays(Far), ct);
        var acknowledgements = await db.DocumentAcknowledgements.CountAsync(ct);

        var current = total - overdue;
        var upcoming = await active
            .Where(d => d.NextReviewDue != null)
            .OrderBy(d => d.NextReviewDue)
            .Take(5)
            .Select(d => new AnalyticsRowDto(
                d.Code, d.Title, d.NextReviewDue!.Value.ToString("yyyy-MM-dd"), d.Status.ToString()))
            .ToListAsync(ct);

        return new DocumentControlStatsDto(
            total, current, Percent(current, total), overdue,
            within30, within60, within90, acknowledgements, upcoming);
    }

    private async Task<NcCapaStatsDto> NcCapaAsync(
        DateOnly today, Guid? branch, Guid? department, CancellationToken ct)
    {
        var ncs = db.Nonconformances.AsQueryable();
        if (branch is { } b) { ncs = ncs.Where(n => n.BranchId == b); }
        if (department is { } d) { ncs = ncs.Where(n => n.DepartmentId == d); }

        var total = await ncs.CountAsync(ct);
        var open = await ncs.CountAsync(n => n.Status != NcStatus.Closed && n.Status != NcStatus.Rejected, ct);

        var capa = ncs.SelectMany(n => n.CapaActions);
        var totalCapa = await capa.CountAsync(ct);
        var overdueCapa = await capa.CountAsync(a => a.Status == CapaActionStatus.Open && a.DueDate < today, ct);

        var byStatus = await CountByAsync(ncs.GroupBy(n => n.Status), ct);
        var bySource = await CountByAsync(ncs.GroupBy(n => n.SourceType), ct);

        // Department names come from the org table; an NC with no department is
        // grouped under an explicit "unassigned" bucket rather than dropped.
        var byDeptRaw = await ncs
            .GroupBy(n => n.DepartmentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var deptNames = await db.Departments
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var byDepartment = byDeptRaw
            .Select(x => new CategoryCountDto(
                x.Key is { } id && deptNames.TryGetValue(id, out var name) ? name : "unassigned", x.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

        var active = await ncs
            .Where(n => n.Status != NcStatus.Closed && n.Status != NcStatus.Rejected)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(5)
            .Select(n => new AnalyticsRowDto(n.NcRef, n.Title, null, n.Status.ToString()))
            .ToListAsync(ct);

        var (closedOnTime, closedTotal) = await CapaClosureAsync(ncs, ct);

        return new NcCapaStatsDto(
            open, total, overdueCapa, totalCapa,
            closedOnTime, closedTotal, Percent(closedOnTime, closedTotal),
            Percent(totalCapa - overdueCapa, totalCapa),
            byStatus, bySource, byDepartment, active);
    }

    /// <summary>
    /// CAPA closure measured against the committed due date. A completed action
    /// records <c>CompletedAtUtc</c> but carries no raised-at stamp, so on-time
    /// performance is the honest measure available — an elapsed-days average would
    /// need a start date this record does not have.
    /// </summary>
    private static async Task<(int OnTime, int Total)> CapaClosureAsync(
        IQueryable<Nonconformance> ncs, CancellationToken ct)
    {
        var closed = await ncs
            .SelectMany(n => n.CapaActions)
            .Where(a => a.Status == CapaActionStatus.Completed && a.CompletedAtUtc != null)
            .Select(a => new { a.DueDate, a.CompletedAtUtc })
            .ToListAsync(ct);

        var onTime = closed.Count(a =>
            DateOnly.FromDateTime(a.CompletedAtUtc!.Value.UtcDateTime) <= a.DueDate);
        return (onTime, closed.Count);
    }

    private async Task<ComplaintsStatsDto> ComplaintsAsync(Guid? branch, Guid? department, CancellationToken ct)
    {
        var complaints = db.Complaints.AsQueryable();
        if (branch is { } b) { complaints = complaints.Where(c => c.BranchId == b); }
        if (department is { } d) { complaints = complaints.Where(c => c.DepartmentId == d); }

        var total = await complaints.CountAsync(ct);
        var open = await complaints.CountAsync(
            c => c.Status != ComplaintStatus.Closed && c.Status != ComplaintStatus.Invalid, ct);
        var byChannel = await CountByAsync(complaints.GroupBy(c => c.Channel), ct);

        // Measured against the tenant's own published commitment. SLA definitions
        // are free-text per module, so this matches the complaints module and takes
        // the most generous target defined for it; with none defined the "within
        // SLA" figure stays null rather than being scored against an assumed one.
        var slaHours = await db.SlaDefinitions
            .Where(s => s.Module.ToLower() == "complaints")
            .Select(s => (int?)s.TargetHours)
            .MaxAsync(ct);

        var resolved = await complaints
            .Where(c => c.Status == ComplaintStatus.Closed || c.Status == ComplaintStatus.Resolved)
            .Select(c => new { c.LoggedAtUtc, c.ModifiedAtUtc })
            .ToListAsync(ct);

        decimal? avgDays = null;
        int withinSla = 0;
        if (resolved.Count > 0)
        {
            var spans = resolved
                .Select(r => ((r.ModifiedAtUtc ?? r.LoggedAtUtc) - r.LoggedAtUtc).TotalDays)
                .ToList();
            avgDays = Math.Round((decimal)spans.Average(), 1);
            if (slaHours is { } targetHours)
            {
                withinSla = spans.Count(s => s <= targetHours / 24d);
            }
        }

        var active = await complaints
            .Where(c => c.Status != ComplaintStatus.Closed && c.Status != ComplaintStatus.Invalid)
            .OrderByDescending(c => c.LoggedAtUtc)
            .Take(5)
            .Select(c => new AnalyticsRowDto(
                c.ComplaintRef, c.Subject, c.Channel.ToString(), c.Status.ToString()))
            .ToListAsync(ct);

        return new ComplaintsStatsDto(
            open, total, withinSla, resolved.Count,
            slaHours is null || resolved.Count == 0 ? null : Percent(withinSla, resolved.Count),
            avgDays, byChannel, active);
    }

    private async Task<AuditStatsDto> AuditsAsync(Guid? branch, Guid? department, CancellationToken ct)
    {
        var audits = db.Audits.AsQueryable();
        if (branch is { } b) { audits = audits.Where(a => a.BranchId == b); }
        if (department is { } d) { audits = audits.Where(a => a.DepartmentId == d); }

        var planned = await audits.CountAsync(ct);
        var completed = await audits.CountAsync(a => a.Status == AuditStatus.SignedOff, ct);

        var findings = audits.SelectMany(a => a.Findings);
        var major = await findings.CountAsync(f => f.Grade == FindingGrade.MajorNc, ct);
        var minor = await findings.CountAsync(f => f.Grade == FindingGrade.MinorNc, ct);
        // "Opportunity for improvement" is this system's observation grade.
        var observations = await findings.CountAsync(f => f.Grade == FindingGrade.Ofi, ct);

        var recent = await audits
            .OrderByDescending(a => a.PlannedDate)
            .Take(5)
            .Select(a => new AnalyticsRowDto(
                a.AuditRef, a.Title, a.PlannedDate.ToString("yyyy-MM-dd"), a.Status.ToString()))
            .ToListAsync(ct);

        return new AuditStatsDto(
            completed, planned, Percent(completed, planned), major, minor, observations, recent);
    }

    private async Task<EquipmentStatsDto> EquipmentAsync(
        DateOnly today, Guid? branch, Guid? department, CancellationToken ct)
    {
        var items = db.EquipmentItems.AsQueryable();
        if (branch is { } b) { items = items.Where(e => e.BranchId == b); }
        if (department is { } d) { items = items.Where(e => e.DepartmentId == d); }

        var total = await items.CountAsync(ct);
        var retired = await items.CountAsync(e => e.Status == EquipmentStatus.Retired, ct);
        var outOfService = await items.CountAsync(e => e.Status == EquipmentStatus.OutOfService, ct);
        var overdue = await items.CountAsync(
            e => e.Status != EquipmentStatus.Retired
                 && e.NextCalibrationDue != null && e.NextCalibrationDue < today, ct);

        // Compliance and availability are both measured over the in-service fleet:
        // retired assets are not a calibration failure.
        var inService = total - retired;
        var calibrationCurrent = inService - overdue;
        var byStatus = await CountByAsync(items.GroupBy(e => e.Status), ct);

        var upcoming = await items
            .Where(e => e.Status != EquipmentStatus.Retired && e.NextCalibrationDue != null)
            .OrderBy(e => e.NextCalibrationDue)
            .Take(5)
            .Select(e => new AnalyticsRowDto(
                e.Code, e.Name, e.NextCalibrationDue!.Value.ToString("yyyy-MM-dd"), e.Status.ToString()))
            .ToListAsync(ct);

        return new EquipmentStatsDto(
            total, calibrationCurrent, Percent(calibrationCurrent, inService),
            outOfService, Percent(inService - outOfService, inService), overdue, byStatus, upcoming);
    }

    private async Task<CompetencyStatsDto> CompetencyAsync(DateOnly today, CancellationToken ct)
    {
        var records = db.Competencies;

        var total = await records.CountAsync(ct);
        var authorized = await records.CountAsync(c => c.Status == CompetencyStatus.Authorized, ct);
        var revoked = await records.CountAsync(c => c.Status == CompetencyStatus.Revoked, ct);
        var pending = await records.CountAsync(c => c.Status == CompetencyStatus.PendingTraining, ct);
        var expiring = await records.CountAsync(
            c => c.Status == CompetencyStatus.Authorized
                 && c.ExpiresAt != null && c.ExpiresAt >= today && c.ExpiresAt <= today.AddDays(Far), ct);

        var recent = await records
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(5)
            .Select(c => new AnalyticsRowDto(
                c.Subject, c.Subject, c.ExpiresAt == null ? null : c.ExpiresAt.Value.ToString("yyyy-MM-dd"),
                c.Status.ToString()))
            .ToListAsync(ct);

        return new CompetencyStatsDto(
            authorized, total, Percent(authorized, total), expiring, revoked, pending, recent);
    }

    private async Task<PtStatsDto> PtAsync(CancellationToken ct)
    {
        var enrollments = db.PtEnrollments;

        var total = await enrollments.CountAsync(ct);
        var satisfactory = await enrollments.CountAsync(p => p.Performance == PtPerformance.Satisfactory, ct);
        var questionable = await enrollments.CountAsync(p => p.Performance == PtPerformance.Questionable, ct);
        var unsatisfactory = await enrollments.CountAsync(p => p.Performance == PtPerformance.Unsatisfactory, ct);
        var pending = await enrollments.CountAsync(p => p.Performance == PtPerformance.Pending, ct);

        // Scored against schemes that have actually returned a result; pending
        // enrolments are not evidence either way.
        var scored = satisfactory + questionable + unsatisfactory;

        var recent = await enrollments
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(5)
            .Select(p => new AnalyticsRowDto(
                p.PtRef, p.Scheme + " · " + p.Analyte,
                p.ZScore == null ? null : "z=" + p.ZScore.ToString(), p.Performance.ToString()))
            .ToListAsync(ct);

        return new PtStatsDto(
            satisfactory, questionable, unsatisfactory, pending, total,
            Percent(satisfactory, scored), recent);
    }

    private async Task<SupplierStatsDto> SuppliersAsync(Guid? branch, Guid? department, CancellationToken ct)
    {
        var suppliers = db.Suppliers.AsQueryable();
        if (branch is { } b) { suppliers = suppliers.Where(s => s.BranchId == b); }
        if (department is { } d) { suppliers = suppliers.Where(s => s.DepartmentId == d); }

        var total = await suppliers.CountAsync(ct);
        var approved = await suppliers.CountAsync(s => s.Status == SupplierStatus.Approved, ct);
        var suspended = await suppliers.CountAsync(s => s.Status == SupplierStatus.Suspended, ct);

        var scores = await db.SupplierEvaluations.Select(e => e.WeightedTotal).ToListAsync(ct);
        decimal? averageScore = scores.Count == 0 ? null : Math.Round(scores.Average(), 1);

        var recent = await suppliers
            .OrderBy(s => s.Name)
            .Take(5)
            .Select(s => new AnalyticsRowDto(s.SupplierRef, s.Name, s.SupplierType, s.Status.ToString()))
            .ToListAsync(ct);

        return new SupplierStatsDto(
            approved, total, Percent(approved, total), suspended, averageScore, recent);
    }

    private async Task<RiskStatsDto> RiskAsync(
        DateOnly today, Guid? branch, Guid? department, CancellationToken ct)
    {
        var risks = db.Risks.AsQueryable();
        if (branch is { } b) { risks = risks.Where(r => r.BranchId == b); }
        if (department is { } d) { risks = risks.Where(r => r.DepartmentId == d); }

        var total = await risks.CountAsync(ct);
        var open = risks.Where(r => r.Status != RiskStatus.Closed);

        // "High or extreme" is the domain's own residual threshold, so the page and
        // the dashboard KPI agree on what counts as high.
        var high = await open.CountAsync(
            r => r.ResidualRpn != null && r.ResidualRpn > RiskItem.HighResidualThreshold, ct);
        var highInitial = await risks.CountAsync(r => r.Rpn > RiskItem.HighResidualThreshold, ct);
        var highMitigated = highInitial - high;

        var overdue = await risks
            .SelectMany(r => r.Actions)
            .CountAsync(a => !a.Completed && a.DueDate < today, ct);

        // The matrix plots residual position where one has been assessed, falling
        // back to the initial assessment so an unmitigated risk still appears.
        var cells = await risks
            .Select(r => new
            {
                Likelihood = r.ResidualLikelihood ?? r.Likelihood,
                Impact = r.ResidualImpact ?? r.Impact,
            })
            .GroupBy(x => new { x.Likelihood, x.Impact })
            .Select(g => new RiskMatrixCellDto(g.Key.Likelihood, g.Key.Impact, g.Count()))
            .ToListAsync(ct);

        var top = await open
            .OrderByDescending(r => r.ResidualRpn ?? r.Rpn)
            .Take(5)
            .Select(r => new AnalyticsRowDto(
                r.RiskRef, r.Title, (r.ResidualRpn ?? r.Rpn).ToString(), r.Status.ToString()))
            .ToListAsync(ct);

        return new RiskStatsDto(
            high, total, highMitigated, Percent(highMitigated, highInitial), overdue, cells, top);
    }

    // ── Composite score ──────────────────────────────────────────────────────

    /// <summary>
    /// Weighted mean of the category scores, using the tenant's configured
    /// weighting. A category contributes only when it is visible to the caller,
    /// carries a non-zero weight, and has a population to score — anything else is
    /// returned with the reason it was excluded, so the number can be reproduced.
    /// </summary>
    private async Task<QualityHealthScoreDto> HealthAsync(
        DocumentControlStatsDto? documents,
        NcCapaStatsDto? ncCapa,
        ComplaintsStatsDto? complaints,
        AuditStatsDto? audits,
        EquipmentStatsDto? equipment,
        CompetencyStatsDto? competency,
        PtStatsDto? pt,
        SupplierStatsDto? suppliers,
        RiskStatsDto? risk,
        CancellationToken ct)
    {
        var profile = await db.QualityHealthProfiles
            .Include(p => p.Weights)
            .FirstOrDefaultAsync(ct);

        // Each category's achieved score is the percentage the section already
        // reports, so the tile the reviewer reads and the score are the same figure.
        var achieved = new Dictionary<QualityHealthCategory, decimal?>
        {
            [QualityHealthCategory.DocumentControl] = documents?.PercentCurrent,
            [QualityHealthCategory.NonconformanceCapa] = ncCapa?.CapaOnSchedulePercent,
            [QualityHealthCategory.Complaints] = complaints?.PercentWithinSla,
            [QualityHealthCategory.InternalAudit] = audits?.PlanCompletionPercent,
            [QualityHealthCategory.Equipment] = equipment?.CalibrationCompliancePercent,
            [QualityHealthCategory.Competency] = competency?.PercentCompetent,
            [QualityHealthCategory.ProficiencyTesting] = pt?.SatisfactionRatePercent,
            [QualityHealthCategory.SupplierQuality] = suppliers?.ApprovedPercent,
            [QualityHealthCategory.Risk] = risk?.HighMitigatedPercent,
        };

        var components = new List<QualityHealthComponentDto>();
        decimal weightedSum = 0;
        var totalWeight = 0;

        foreach (var category in Enum.GetValues<QualityHealthCategory>())
        {
            var weight = profile?.WeightFor(category) ?? QualityHealthProfile.DefaultWeight;
            var score = achieved[category];

            string? excluded = null;
            if (score is null)
            {
                excluded = achieved.ContainsKey(category) && IsHidden(category) ? "notPermitted" : "noData";
            }
            else if (weight == 0)
            {
                excluded = "zeroWeight";
            }

            var contributed = excluded is null;
            if (contributed)
            {
                weightedSum += score!.Value * weight;
                totalWeight += weight;
            }

            components.Add(new QualityHealthComponentDto(
                category.ToString(), weight, score, contributed, excluded));
        }

        decimal? composite = totalWeight == 0 ? null : Math.Round(weightedSum / totalWeight, 1);
        return new QualityHealthScoreDto(
            composite, components, components.Count(c => c.Contributed), components.Count);

        bool IsHidden(QualityHealthCategory category) => category switch
        {
            QualityHealthCategory.DocumentControl => documents is null,
            QualityHealthCategory.NonconformanceCapa => ncCapa is null,
            QualityHealthCategory.Complaints => complaints is null,
            QualityHealthCategory.InternalAudit => audits is null,
            QualityHealthCategory.Equipment => equipment is null,
            QualityHealthCategory.Competency => competency is null,
            QualityHealthCategory.ProficiencyTesting => pt is null,
            QualityHealthCategory.SupplierQuality => suppliers is null,
            QualityHealthCategory.Risk => risk is null,
            _ => false,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A percentage of a real population, or null when the population is empty.
    /// Never zero-for-empty: "none due" and "none done" must not look alike.
    /// </summary>
    private static decimal? Percent(int part, int whole) =>
        whole <= 0 ? null : Math.Round(part * 100m / whole, 1);

    /// <summary>
    /// Groups and counts server-side, then names the buckets in memory — the
    /// enum-to-string projection is not SQL-translatable, and the grouped result is
    /// at most a handful of rows.
    /// </summary>
    private static async Task<IReadOnlyList<CategoryCountDto>> CountByAsync<TKey, TSource>(
        IQueryable<IGrouping<TKey, TSource>> grouped, CancellationToken ct)
        where TKey : notnull
    {
        var rows = await grouped
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows
            .Select(r => new CategoryCountDto(r.Key.ToString() ?? "unknown", r.Count))
            .OrderByDescending(r => r.Count)
            .ToList();
    }
}
