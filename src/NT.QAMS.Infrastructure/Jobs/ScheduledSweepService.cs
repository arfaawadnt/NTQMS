using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure.Jobs;

/// <summary>
/// The daily compliance sweep: proposes calibration-due, grace-lockout, and
/// competency-expiry transitions across ALL tenants (IgnoreQueryFilters — this
/// is one of the few legitimate cross-tenant read paths, and it only ever calls
/// guarded aggregate methods: the sweep proposes, the aggregate decides).
/// Domain events raised by the transitions flow through the outbox as usual,
/// each stamped with its own row's tenant. Idempotent by construction: a
/// declined proposal is a no-op, so re-runs are harmless.
/// </summary>
public sealed partial class ScheduledSweepService(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<ScheduledSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so migrations/bootstrap finish first.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (due, locked, expired, suspended) = await RunSweepAsync(stoppingToken);
                if (due + locked + expired + suspended > 0)
                {
                    LogSweep(logger, due, locked, expired, suspended);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSweepFailed(logger, ex);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task<(int Due, int Locked, int Expired, int Suspended)> RunSweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        // Trusted cross-tenant sweep: elevate before the first query so the
        // connection runs with RLS bypass for this unit of work only.
        scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Elevate();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // OPS-002: leader election — with more than one instance, exactly one
        // runs a sweep round; the others skip (the next interval retries).
        var result = (Due: 0, Locked: 0, Expired: 0, Suspended: 0);
        var ran = await AdvisoryLock.TryRunExclusiveAsync(
            db, AdvisoryLockKeys.ComplianceSweep,
            async () =>
            {
                using var activity = Observability.QamsDiagnostics.Jobs.StartActivity("job compliance-sweep");
                result = await SweepAsync(db, ct);
            }, ct);
        if (ran)
        {
            // OBS-003: liveness gauge — the "sweep hasn't run" alert source.
            Observability.QamsMetrics.RecordJobSuccess("compliance-sweep", clock.UtcNow);
        }

        return result;
    }

    private async Task<(int Due, int Locked, int Expired, int Suspended)> SweepAsync(AppDbContext db, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var dueCandidates = await db.EquipmentItems
            .IgnoreQueryFilters()
            .Where(e => e.Status == EquipmentStatus.Active
                        && e.NextCalibrationDue != null && e.NextCalibrationDue <= today)
            .ToListAsync(ct);
        dueCandidates.ForEach(e => e.MarkCalibrationDue(today));

        var lockCandidates = await db.EquipmentItems
            .IgnoreQueryFilters()
            .Where(e => e.Status == EquipmentStatus.NeedsCalibration && e.NextCalibrationDue != null)
            .ToListAsync(ct);
        lockCandidates.ForEach(e => e.LockOutIfGraceExhausted(today));
        var locked = lockCandidates.Count(e => e.Status == EquipmentStatus.OutOfService);

        var expiryCandidates = await db.Competencies
            .IgnoreQueryFilters()
            .Where(c => c.Status == CompetencyStatus.Authorized
                        && c.ExpiresAt != null && c.ExpiresAt <= today)
            .ToListAsync(ct);
        expiryCandidates.ForEach(c => c.ExpireIfDue(today));

        var authorizationCandidates = await db.TestAuthorizations
            .IgnoreQueryFilters()
            .Where(a => (a.Status == TestAuthorizationStatus.Active
                         || a.Status == TestAuthorizationStatus.Suspended)
                        && a.ExpiresOn <= today)
            .ToListAsync(ct);
        authorizationCandidates.ForEach(a => a.ExpireIfDue(today));

        var supplierCandidates = await db.Suppliers
            .IgnoreQueryFilters()
            .Include(s => s.Certificates)
            .Where(s => s.Status == SupplierStatus.Approved
                        && s.Certificates.Any(c => c.ExpiresAt < today))
            .ToListAsync(ct);
        supplierCandidates.ForEach(s => s.SuspendIfCertificateExpired(today));
        var suspended = supplierCandidates.Count(s => s.Status == SupplierStatus.Suspended);

        var standardCandidates = await db.ReferenceStandards
            .IgnoreQueryFilters()
            .Where(s => s.Status == ReferenceStandardStatus.Active
                        && s.ExpiresOn != null && s.ExpiresOn <= today)
            .ToListAsync(ct);
        standardCandidates.ForEach(s => s.MarkExpiredIfReached(today));

        var reviewCandidates = await db.Documents
            .IgnoreQueryFilters()
            .Where(d => d.Status == Domain.DocumentControl.DocumentStatus.Published
                        && !d.ReviewDueRaised
                        && d.NextReviewDue != null && d.NextReviewDue <= today)
            .ToListAsync(ct);
        reviewCandidates.ForEach(d => d.MarkReviewDueIfReached(today));

        var now = clock.UtcNow;
        var timerCandidates = await db.EscalationTimers
            .IgnoreQueryFilters()
            .Where(t => t.Active && t.NextStepAtUtc != null && t.NextStepAtUtc <= now)
            .ToListAsync(ct);
        timerCandidates.ForEach(t => t.AdvanceIfDue(now));

        await db.SaveChangesAsync(ct);
        return (dueCandidates.Count, locked, expiryCandidates.Count, suspended);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Sweep: {Due} calibration(s) due, {Locked} lockout(s), {Expired} competency expiry(ies), {Suspended} supplier suspension(s)")]
    private static partial void LogSweep(ILogger logger, int due, int locked, int expired, int suspended);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);
}
