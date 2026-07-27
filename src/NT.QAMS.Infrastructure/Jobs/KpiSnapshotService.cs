using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Reporting;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Jobs;

/// <summary>
/// Projection sweep for read.kpi_snapshot: one row per tenant per day, upserted
/// from real operational rows (IgnoreQueryFilters — the sweep is a legitimate
/// cross-tenant read path). This is what makes trend charts real history;
/// per the reporting architecture, fabricated back-fill is banned, so history
/// simply starts the day this service first runs. Idempotent: re-runs update
/// today's row in place.
/// </summary>
public sealed partial class KpiSnapshotService(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<KpiSnapshotService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so migrations/bootstrap finish first.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tenants = await SnapshotAllTenantsAsync(stoppingToken);
                LogSnapshot(logger, tenants);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSnapshotFailed(logger, ex);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task<int> SnapshotAllTenantsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        // Trusted cross-tenant sweep: elevate before the first query so the
        // connection runs with RLS bypass for this unit of work only.
        scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Elevate();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // OPS-002: leader election — with more than one instance, exactly one
        // upserts a snapshot round; the others skip (the next interval retries).
        var tenants = 0;
        await AdvisoryLock.TryRunExclusiveAsync(
            db, AdvisoryLockKeys.KpiSnapshot,
            async () => tenants = await SnapshotAsync(db, ct), ct);
        return tenants;
    }

    private async Task<int> SnapshotAsync(AppDbContext db, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var tenantIds = await db.Tenants
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var tenantId in tenantIds)
        {
            var snapshot = await db.KpiSnapshots
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Date == today, ct);
            if (snapshot is null)
            {
                snapshot = new KpiSnapshot { TenantId = tenantId, Date = today };
                db.KpiSnapshots.Add(snapshot);
            }

            snapshot.OpenNcs = await db.Nonconformances.IgnoreQueryFilters()
                .CountAsync(n => n.TenantId == tenantId
                                 && n.Status != NcStatus.Closed && n.Status != NcStatus.Rejected, ct);
            snapshot.OverdueCapaActions = await db.Nonconformances.IgnoreQueryFilters()
                .Where(n => n.TenantId == tenantId)
                .SelectMany(n => n.CapaActions)
                .CountAsync(a => a.Status == CapaActionStatus.Open && a.DueDate < today, ct);
            snapshot.OpenComplaints = await db.Complaints.IgnoreQueryFilters()
                .CountAsync(c => c.TenantId == tenantId
                                 && c.Status != ComplaintStatus.Closed && c.Status != ComplaintStatus.Invalid, ct);
            snapshot.AuditsInProgress = await db.Audits.IgnoreQueryFilters()
                .CountAsync(a => a.TenantId == tenantId
                                 && a.Status == Domain.AuditManagement.AuditStatus.InProgress, ct);
            snapshot.EquipmentOutOfService = await db.EquipmentItems.IgnoreQueryFilters()
                .CountAsync(e => e.TenantId == tenantId && e.Status == EquipmentStatus.OutOfService, ct);
            snapshot.HighResidualRisks = await db.Risks.IgnoreQueryFilters()
                .CountAsync(r => r.TenantId == tenantId && r.Status != RiskStatus.Closed
                                 && r.ResidualRpn != null && r.ResidualRpn > RiskItem.HighResidualThreshold, ct);
            snapshot.OverdueTasks = await db.WorkTasks.IgnoreQueryFilters()
                .CountAsync(t => t.TenantId == tenantId
                                 && t.Status == WorkTaskStatus.Pending && t.DueDate < today, ct);
            snapshot.PtUnsatisfactory = await db.PtEnrollments.IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenantId && p.Performance == PtPerformance.Unsatisfactory, ct);
        }

        await db.SaveChangesAsync(ct);
        return tenantIds.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "KPI snapshots upserted for {Tenants} tenant(s)")]
    private static partial void LogSnapshot(ILogger logger, int tenants);

    [LoggerMessage(Level = LogLevel.Error, Message = "KPI snapshot sweep failed")]
    private static partial void LogSnapshotFailed(ILogger logger, Exception ex);
}
