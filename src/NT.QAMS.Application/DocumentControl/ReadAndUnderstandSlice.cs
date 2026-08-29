using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.DocumentControl;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.DocumentControl;

// ── Configure the read-and-understand distribution ───────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Documents, PermissionAction.Edit)]
public sealed record SetDocumentReadAndUnderstandCommand(
    Guid DocumentId, bool Required, DocumentAudienceScope Scope, IReadOnlyList<Guid> DepartmentIds) : ICommand;

public sealed class SetDocumentReadAndUnderstandHandler(IAppDbContext db)
    : ICommandHandler<SetDocumentReadAndUnderstandCommand>
{
    public async Task Handle(SetDocumentReadAndUnderstandCommand c, CancellationToken ct)
    {
        var doc = await db.Documents
            .Include(d => d.AudienceDepartments)
            .SingleOrDefaultAsync(d => d.Id == c.DocumentId, ct)
            ?? throw new DomainException("DOC-404", "Document not found.");

        doc.SetReadAndUnderstand(c.Required, c.Scope, c.DepartmentIds ?? []);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record GetDocumentReadAndUnderstandQuery(Guid DocumentId) : IQuery<ReadAndUnderstandDto>;

public sealed class GetDocumentReadAndUnderstandHandler(IAppDbContext db)
    : IQueryHandler<GetDocumentReadAndUnderstandQuery, ReadAndUnderstandDto>
{
    public async Task<ReadAndUnderstandDto> Handle(GetDocumentReadAndUnderstandQuery q, CancellationToken ct)
    {
        var doc = await db.Documents
            .AsNoTracking()
            .Include(d => d.AudienceDepartments)
            .SingleOrDefaultAsync(d => d.Id == q.DocumentId, ct)
            ?? throw new DomainException("DOC-404", "Document not found.");

        return new ReadAndUnderstandDto(
            doc.RequiresAcknowledgement, doc.AudienceScope.ToString(),
            doc.AudienceDepartments.Select(a => a.DepartmentId).ToList());
    }
}

// ── Per-document compliance: who is expected to read it, and who has ──────────

public sealed record GetDocumentComplianceQuery(Guid DocumentId) : IQuery<DocumentComplianceDto>;

public sealed class GetDocumentComplianceHandler(IAppDbContext db, ICurrentTenant tenant)
    : IQueryHandler<GetDocumentComplianceQuery, DocumentComplianceDto>
{
    public async Task<DocumentComplianceDto> Handle(GetDocumentComplianceQuery q, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        var doc = await db.Documents
            .AsNoTracking()
            .Include(d => d.Versions)
            .Include(d => d.AudienceDepartments)
            .SingleOrDefaultAsync(d => d.Id == q.DocumentId, ct)
            ?? throw new DomainException("DOC-404", "Document not found.");

        var publishedLabel = doc.PublishedVersion?.VersionLabel;
        var targetDepartments = doc.AudienceDepartments.Select(a => a.DepartmentId).ToHashSet();

        var audience = await ReadAndUnderstandAudience
            .ResolveAsync(db, tenantId, doc.RequiresAcknowledgement, doc.AudienceScope, targetDepartments, ct);

        // Acknowledgements of the current published version, by user.
        var ackByUser = publishedLabel is null
            ? new Dictionary<Guid, DateTimeOffset>()
            : (await db.DocumentAcknowledgements.AsNoTracking()
                    .Where(a => a.DocumentId == doc.Id && a.VersionLabel == publishedLabel)
                    .Select(a => new { a.UserId, a.AcknowledgedAtUtc })
                    .ToListAsync(ct))
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.AcknowledgedAtUtc));

        var readers = audience
            .Select(u => new AudienceReaderDto(
                u.Id, u.DisplayName,
                ackByUser.ContainsKey(u.Id),
                ackByUser.TryGetValue(u.Id, out var at) ? at : null))
            .OrderBy(r => r.Acknowledged).ThenBy(r => r.UserDisplay)
            .ToList();

        var acknowledged = readers.Count(r => r.Acknowledged);
        var percent = readers.Count == 0 ? 0m : decimal.Round(acknowledged * 100m / readers.Count, 1);

        return new DocumentComplianceDto(
            doc.Id, doc.Code, doc.Title, publishedLabel,
            readers.Count, acknowledged, readers.Count - acknowledged, percent, readers);
    }
}

// ── Tenant-wide dashboard: every mandatory published document ─────────────────

public sealed record GetReadAndUnderstandDashboardQuery : IQuery<IReadOnlyList<ReadAndUnderstandDashboardRowDto>>;

public sealed class GetReadAndUnderstandDashboardHandler(IAppDbContext db, ICurrentTenant tenant)
    : IQueryHandler<GetReadAndUnderstandDashboardQuery, IReadOnlyList<ReadAndUnderstandDashboardRowDto>>
{
    public async Task<IReadOnlyList<ReadAndUnderstandDashboardRowDto>> Handle(
        GetReadAndUnderstandDashboardQuery q, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        var docs = await db.Documents
            .AsNoTracking()
            .Include(d => d.Versions)
            .Include(d => d.AudienceDepartments)
            .Where(d => d.RequiresAcknowledgement && d.Status == DocumentStatus.Published)
            .ToListAsync(ct);

        if (docs.Count == 0)
        {
            return [];
        }

        // Resolve the two possible audiences once; per-department docs are filtered in memory.
        var allActive = await ReadAndUnderstandAudience.ActiveUsersAsync(db, tenantId, ct);

        // M-10: ONE acknowledgement query for the whole document set — the
        // per-document query in the loop was a textbook N+1.
        var docIds = docs.Select(d => d.Id).ToList();
        var acksByDocVersion = (await db.DocumentAcknowledgements.AsNoTracking()
                .Where(a => docIds.Contains(a.DocumentId))
                .Select(a => new { a.DocumentId, a.VersionLabel, a.UserId })
                .Distinct()
                .ToListAsync(ct))
            .GroupBy(a => (a.DocumentId, a.VersionLabel))
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToHashSet());

        var rows = new List<ReadAndUnderstandDashboardRowDto>(docs.Count);
        foreach (var doc in docs)
        {
            var publishedLabel = doc.PublishedVersion?.VersionLabel;
            var targets = doc.AudienceDepartments.Select(a => a.DepartmentId).ToHashSet();
            var audience = doc.AudienceScope == DocumentAudienceScope.AllStaff
                ? allActive
                : allActive.Where(u => u.DepartmentIds.Any(targets.Contains)).ToList();

            var ackedUsers = publishedLabel is null
                ? []
                : acksByDocVersion.GetValueOrDefault((doc.Id, publishedLabel)) ?? new HashSet<Guid>();

            var audienceCount = audience.Count;
            var acknowledged = audience.Count(u => ackedUsers.Contains(u.Id));
            var percent = audienceCount == 0 ? 0m : decimal.Round(acknowledged * 100m / audienceCount, 1);

            rows.Add(new ReadAndUnderstandDashboardRowDto(
                doc.Id, doc.Code, doc.Title, doc.AudienceScope.ToString(), publishedLabel,
                audienceCount, acknowledged, audienceCount - acknowledged, percent));
        }

        return rows.OrderBy(r => r.CompliancePercent).ThenBy(r => r.Code).ToList();
    }
}

/// <summary>
/// Resolves the set of active users expected to read a document. All-staff means every
/// active user in the tenant; by-department means active users whose department access
/// intersects the target set. Unrestricted users (no department access) are treated as
/// all-staff and are therefore excluded from a by-department audience, keeping the
/// outstanding list to staff genuinely attached to the named departments.
/// </summary>
internal static class ReadAndUnderstandAudience
{
    internal sealed record AudienceUser(Guid Id, string DisplayName, IReadOnlyList<Guid> DepartmentIds);

    public static async Task<IReadOnlyList<AudienceUser>> ActiveUsersAsync(
        IAppDbContext db, Guid tenantId, CancellationToken ct)
    {
        // tenant-bounded: explicit tenant predicate satisfies UserAccountTenantBoundTests.
        var users = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                Departments = u.DepartmentAccess.Select(d => d.DepartmentId).ToList(),
            })
            .ToListAsync(ct);

        return users.Select(u => new AudienceUser(u.Id, u.DisplayName, u.Departments)).ToList();
    }

    public static async Task<IReadOnlyList<AudienceUser>> ResolveAsync(
        IAppDbContext db, Guid tenantId, bool required, DocumentAudienceScope scope,
        IReadOnlySet<Guid> targetDepartments, CancellationToken ct)
    {
        if (!required)
        {
            return [];
        }

        var active = await ActiveUsersAsync(db, tenantId, ct);
        return scope == DocumentAudienceScope.AllStaff
            ? active
            : active.Where(u => u.DepartmentIds.Any(targetDepartments.Contains)).ToList();
    }
}
