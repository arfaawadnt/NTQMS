using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Reporting;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.Reporting;

// ── Live dashboard KPIs ──────────────────────────────────────────────────────

public sealed record GetDashboardKpisQuery : IQuery<DashboardKpisDto>;

/// <summary>
/// Computes every KPI from real operational rows (tenant-scoped by the global
/// query filters). No cached numbers, no fabricated data — the freshness stamp
/// is the computation instant.
/// </summary>
public sealed class GetDashboardKpisHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetDashboardKpisQuery, DashboardKpisDto>
{
    public async Task<DashboardKpisDto> Handle(GetDashboardKpisQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var openNcs = await db.Nonconformances
            .CountAsync(n => n.Status != NcStatus.Closed && n.Status != NcStatus.Rejected, ct);
        var overdueCapa = await db.Nonconformances
            .SelectMany(n => n.CapaActions)
            .CountAsync(a => a.Status == CapaActionStatus.Open && a.DueDate < today, ct);
        var openComplaints = await db.Complaints
            .CountAsync(c => c.Status != ComplaintStatus.Closed && c.Status != ComplaintStatus.Invalid, ct);
        var auditsInProgress = await db.Audits.CountAsync(a => a.Status == AuditStatus.InProgress, ct);
        var outOfService = await db.EquipmentItems
            .CountAsync(e => e.Status == EquipmentStatus.OutOfService, ct);
        var needsCalibration = await db.EquipmentItems
            .CountAsync(e => e.Status == EquipmentStatus.NeedsCalibration, ct);
        var highResidual = await db.Risks
            .CountAsync(r => r.Status != RiskStatus.Closed
                             && r.ResidualRpn != null && r.ResidualRpn > RiskItem.HighResidualThreshold, ct);
        var overdueTasks = await db.WorkTasks
            .CountAsync(t => t.Status == WorkTaskStatus.Pending && t.DueDate < today, ct);
        var ptUnsatisfactory = await db.PtEnrollments
            .CountAsync(p => p.Performance == PtPerformance.Unsatisfactory, ct);
        var pendingTraining = await db.TrainingAssignments.CountAsync(t => !t.Completed, ct);
        var suspendedSuppliers = await db.Suppliers
            .CountAsync(s => s.Status == SupplierStatus.Suspended, ct);
        var publishedDocs = await db.Documents
            .CountAsync(d => d.Status == DocumentStatus.Published, ct);

        // The population behind each KPI, so the dashboard can state a proportion
        // rather than a bare count. Real row counts only — a KPI without a
        // genuine population would be left un-metered rather than given a
        // plausible-looking denominator.
        var totals = new DashboardKpiTotalsDto(
            Nonconformances: await db.Nonconformances.CountAsync(ct),
            CapaActions: await db.Nonconformances.SelectMany(n => n.CapaActions).CountAsync(ct),
            Complaints: await db.Complaints.CountAsync(ct),
            Audits: await db.Audits.CountAsync(ct),
            EquipmentItems: await db.EquipmentItems.CountAsync(ct),
            Risks: await db.Risks.CountAsync(ct),
            WorkTasks: await db.WorkTasks.CountAsync(ct),
            PtEnrollments: await db.PtEnrollments.CountAsync(ct),
            TrainingAssignments: await db.TrainingAssignments.CountAsync(ct),
            Suppliers: await db.Suppliers.CountAsync(ct),
            Documents: await db.Documents.CountAsync(ct));

        return new DashboardKpisDto(
            openNcs, overdueCapa, openComplaints, auditsInProgress,
            outOfService, needsCalibration, highResidual, overdueTasks,
            ptUnsatisfactory, pendingTraining, suspendedSuppliers, publishedDocs, now, totals);
    }
}

// ── KPI history (real snapshots only) ────────────────────────────────────────

public sealed record GetKpiHistoryQuery(int Days = 90) : IQuery<IReadOnlyList<KpiHistoryPointDto>>;

/// <summary>
/// Reads the daily snapshot rows accumulated by the KPI sweep. History starts
/// when the sweep first ran — an empty or short series is shown honestly rather
/// than back-filled with fabricated points.
/// </summary>
public sealed class GetKpiHistoryHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetKpiHistoryQuery, IReadOnlyList<KpiHistoryPointDto>>
{
    public async Task<IReadOnlyList<KpiHistoryPointDto>> Handle(GetKpiHistoryQuery query, CancellationToken ct)
    {
        var from = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)
            .AddDays(-Math.Clamp(query.Days, 1, 366));
        return await db.KpiSnapshots
            .Where(s => s.Date >= from)
            .OrderBy(s => s.Date)
            .Select(s => new KpiHistoryPointDto(
                s.Date, s.OpenNcs, s.OverdueCapaActions, s.OpenComplaints,
                s.EquipmentOutOfService, s.HighResidualRisks, s.OverdueTasks))
            .ToListAsync(ct);
    }
}

// ── NC Pareto ────────────────────────────────────────────────────────────────

public sealed record GetNcParetoQuery : IQuery<IReadOnlyList<NcParetoBucketDto>>;

public sealed class GetNcParetoHandler(IAppDbContext db)
    : IQueryHandler<GetNcParetoQuery, IReadOnlyList<NcParetoBucketDto>>
{
    public async Task<IReadOnlyList<NcParetoBucketDto>> Handle(GetNcParetoQuery query, CancellationToken ct)
    {
        // Group/count server-side; the enum-to-name projection is not
        // SQL-translatable, so it happens on the (small) grouped result.
        var buckets = await db.Nonconformances
            .GroupBy(n => n.SourceType)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return buckets
            .Select(b => new NcParetoBucketDto(b.Key.ToString(), b.Count))
            .OrderByDescending(b => b.Count)
            .ToList();
    }
}

// ── SLA compliance ───────────────────────────────────────────────────────────

public sealed record GetSlaComplianceQuery : IQuery<SlaComplianceDto>;

/// <summary>
/// Work-task on-time performance: a completed task is on time when its
/// completion stamp falls on or before its due date (end of day, UTC).
/// </summary>
public sealed class GetSlaComplianceHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetSlaComplianceQuery, SlaComplianceDto>
{
    public async Task<SlaComplianceDto> Handle(GetSlaComplianceQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var completed = await db.WorkTasks
            .Where(t => t.Status == WorkTaskStatus.Completed && t.CompletedAtUtc != null)
            .Select(t => new { t.DueDate, t.CompletedAtUtc })
            .ToListAsync(ct);
        var onTime = completed.Count(t =>
            DateOnly.FromDateTime(t.CompletedAtUtc!.Value.UtcDateTime) <= t.DueDate);

        var openTotal = await db.WorkTasks.CountAsync(t => t.Status == WorkTaskStatus.Pending, ct);
        var openOverdue = await db.WorkTasks
            .CountAsync(t => t.Status == WorkTaskStatus.Pending && t.DueDate < today, ct);

        var percent = completed.Count == 0 ? 0m : Math.Round(onTime * 100m / completed.Count, 1);
        return new SlaComplianceDto(completed.Count, onTime, percent, openTotal, openOverdue, now);
    }
}
