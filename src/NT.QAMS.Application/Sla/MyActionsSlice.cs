using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Operations;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.Sla;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Sla;

/// <summary>
/// The unified "My Tasks" action centre: every action across the system that awaits
/// the signed-in user, in one feed — so a user has a single place to see what needs
/// doing rather than hunting each module. It is a <b>live read model</b>, not a
/// materialised queue: each source is projected from its aggregate's current state
/// at query time, so the feed can never drift from reality and needs no event
/// plumbing or migration.
/// <para>
/// Sources included (v1.53): manual work tasks (completable inline), nonconformances
/// assigned to the user, CAPA actions they own, NC verifications/closures they may
/// sign (SoD: never their own NC), risk treatments and quality objectives they own,
/// and management reviews they are a participant of. Cross-module items are shown
/// only while they need action (pending); the user's own manual tasks keep their
/// completed history, preserving URS-115. Further sources can be added as providers
/// without changing the contract.
/// </para>
/// </summary>
public sealed record GetMyActionsQuery : IQuery<IReadOnlyList<MyActionDto>>;

public sealed class GetMyActionsHandler(
    IAppDbContext db, ICurrentUser user, IUserPrivileges privileges, IClock clock)
    : IQueryHandler<GetMyActionsQuery, IReadOnlyList<MyActionDto>>
{
    /// <summary>Per-source ceiling — a personal feed, not a bulk-data channel.</summary>
    private const int PerSourceCap = 200;
    private const int HighRpnThreshold = 12;

    public async Task<IReadOnlyList<MyActionDto>> Handle(GetMyActionsQuery q, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var items = new List<MyActionDto>();

        // ── Manual work tasks (direct or role-routed) — completable inline ──────
        // Both role vocabularies, resolved from the database (the JWT tier claim
        // goes stale on reassignment); pending and completed both kept (URS-115).
        var tier = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Role.ToString()).SingleAsync(ct);
        var dynamicRole = privileges.RoleName ?? string.Empty;
        var tasks = await db.WorkTasks.AsNoTracking()
            .Where(t => t.AssigneeUserId == userId
                        || t.AssigneeRole == tier
                        || (dynamicRole != "" && t.AssigneeRole == dynamicRole))
            .OrderBy(t => t.Status == WorkTaskStatus.Pending ? 0 : 1)
            .ThenBy(t => t.DueDate)
            .Take(PerSourceCap)
            .Select(t => new MyActionDto(
                t.Id, "task", t.SubjectRef ?? "—", t.Subject, "task",
                t.DueDate, t.Status == WorkTaskStatus.Pending && t.DueDate < today,
                "normal", t.Status.ToString(), null))
            .ToListAsync(ct);
        items.AddRange(tasks);

        // ── Nonconformances assigned to me (investigation / CAPA planning) ──────
        items.AddRange(await db.Nonconformances.AsNoTracking()
            .Where(n => n.AssignedTo == userId
                        && (n.Status == NcStatus.Assigned || n.Status == NcStatus.Rca
                            || n.Status == NcStatus.ActionPlan))
            .OrderByDescending(n => n.Rpn).Take(PerSourceCap)
            .Select(n => new MyActionDto(
                null, "nc", n.NcRef, n.Title, "investigate",
                null, false, n.Rpn > HighRpnThreshold ? "high" : "normal", "Pending",
                "/nonconformances/" + n.Id))
            .ToListAsync(ct));

        // ── CAPA actions I own that are still open ──────────────────────────────
        // Project the owner + its matching child rows, then flatten in memory:
        // EF InMemory cannot translate SelectMany over an owned collection, but it
        // handles a terminal sub-collection projection on both it and PostgreSQL.
        var capaOwners = await db.Nonconformances.AsNoTracking()
            .Where(n => n.CapaActions.Any(a => a.OwnerId == userId && a.Status == CapaActionStatus.Open))
            .Take(PerSourceCap)
            .Select(n => new
            {
                n.Id,
                n.NcRef,
                Actions = n.CapaActions
                    .Where(a => a.OwnerId == userId && a.Status == CapaActionStatus.Open)
                    .Select(a => new { a.Details, a.DueDate }).ToList(),
            })
            .ToListAsync(ct);
        items.AddRange(capaOwners.SelectMany(n => n.Actions.Select(a => new MyActionDto(
            null, "capa", n.NcRef, a.Details, "completeCapa",
            a.DueDate, a.DueDate < today, "normal", "Pending", "/nonconformances/" + n.Id))));

        // ── NC verifications / closures I may sign (never my own NC — SoD) ──────
        if (privileges.IsPlatformAdmin || privileges.Has(PermissionCatalog.Key(
                PermissionCatalog.Nonconformances, PermissionAction.Sign)))
        {
            items.AddRange(await db.Nonconformances.AsNoTracking()
                .Where(n => n.RaisedBy != userId
                            && (n.Status == NcStatus.PendingVerification
                                || n.Status == NcStatus.EffectivenessCheck))
                .OrderByDescending(n => n.Rpn).Take(PerSourceCap)
                .Select(n => new MyActionDto(
                    null, "nc", n.NcRef, n.Title,
                    n.Status == NcStatus.PendingVerification ? "verify" : "confirm",
                    null, false, n.Rpn > HighRpnThreshold ? "high" : "normal", "Pending",
                    "/nonconformances/" + n.Id))
                .ToListAsync(ct));
        }

        // ── Risk mitigation actions I own that are not yet complete ─────────────
        // Ownership lives on the mitigation action, not the risk aggregate. Same
        // owner-then-flatten pattern as CAPA (InMemory cannot SelectMany owned rows).
        var riskOwners = await db.Risks.AsNoTracking()
            .Where(r => r.Status != RiskStatus.Closed
                        && r.Actions.Any(a => a.OwnerId == userId && !a.Completed))
            .Take(PerSourceCap)
            .Select(r => new
            {
                r.Id,
                r.RiskRef,
                Priority = (r.ResidualRpn ?? r.Rpn) > HighRpnThreshold ? "high" : "normal",
                Actions = r.Actions
                    .Where(a => a.OwnerId == userId && !a.Completed)
                    .Select(a => new { a.Description, a.DueDate }).ToList(),
            })
            .ToListAsync(ct);
        items.AddRange(riskOwners.SelectMany(r => r.Actions.Select(a => new MyActionDto(
            null, "risk", r.RiskRef, a.Description, "treatRisk",
            a.DueDate, a.DueDate < today, r.Priority, "Pending", "/risks/" + r.Id))));

        // ── Quality objectives I own that are still active ──────────────────────
        items.AddRange(await db.QualityObjectives.AsNoTracking()
            .Where(o => o.OwnerId == userId && o.Status == ObjectiveStatus.Active)
            .Take(PerSourceCap)
            .Select(o => new MyActionDto(
                null, "objective", o.ObjectiveRef, o.Title, "advanceObjective",
                null, false, "normal", "Pending", "/quality-objectives/" + o.Id))
            .ToListAsync(ct));

        // ── Management reviews I am a participant of, still scheduled ────────────
        items.AddRange(await db.ManagementReviews.AsNoTracking()
            .Where(m => m.Status == ReviewStatus.Scheduled
                        && m.ParticipantUsers.Any(p => p.UserId == userId))
            .Take(PerSourceCap)
            .Select(m => new MyActionDto(
                null, "review", m.ReviewRef, m.Title, "attendReview",
                m.ReviewDate, m.ReviewDate < today, "normal", "Pending",
                "/management-reviews/" + m.Id))
            .ToListAsync(ct));

        // Pending first; overdue before on-track; earliest due first (no due last).
        return items
            .OrderBy(i => i.Status == "Pending" ? 0 : 1)
            .ThenByDescending(i => i.Overdue)
            .ThenBy(i => i.DueDate ?? DateOnly.MaxValue)
            .ThenBy(i => i.Category)
            .ToList();
    }
}
