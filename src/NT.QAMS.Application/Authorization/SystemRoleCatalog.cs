using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.IdentityAccess;

namespace NT.QAMS.Application.Authorization;

/// <summary>
/// The roles every tenant starts with, and the bridge from the fixed role tiers
/// the system used before privileges became configurable.
/// <para>
/// The seeded sets are chosen to reproduce the access each tier already had, so
/// enabling configurable privileges changes nobody's reach on the day it ships. A
/// laboratory then edits these roles, or adds its own, and the system follows.
/// </para>
/// <para>
/// Seeding is additive and idempotent: a role the tenant already has (by name) is
/// left exactly as the tenant configured it. Re-running this must never quietly
/// restore privileges an administrator deliberately removed.
/// </para>
/// </summary>
public static class SystemRoleCatalog
{
    /// <summary>Names of the seeded roles. Stable — they are referenced by data.</summary>
    public const string TenantAdministrator = "Tenant Administrator";
    public const string QualityManager = "Quality Manager";
    public const string DepartmentHead = "Department Head";
    public const string Analyst = "Analyst";
    public const string ExternalAuditor = "External Auditor";

    private static readonly PermissionAction[] ReadActions = [PermissionAction.View, PermissionAction.Export];

    /// <summary>
    /// Maps the pre-existing fixed tier to the seeded role that reproduces it, so
    /// existing accounts land on an equivalent role rather than on nothing.
    /// </summary>
    public static string RoleNameFor(UserRole tier) => tier switch
    {
        UserRole.TenantAdmin => TenantAdministrator,
        UserRole.QualityManager => QualityManager,
        UserRole.DepartmentHead => DepartmentHead,
        UserRole.Analyst => Analyst,
        UserRole.ExternalAuditor => ExternalAuditor,
        // Platform administrators are not tenant members and hold no tenant role.
        _ => TenantAdministrator,
    };

    /// <summary>
    /// Creates any of the seeded roles the tenant does not have yet, returning the
    /// roles that were added. Caller saves.
    /// </summary>
    public static async Task<IReadOnlyList<Role>> SeedMissingAsync(
        IAppDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        // Callable from platform-level flows (provisioning, startup backfill)
        // where no request tenant is resolved - scope explicitly, like the LOV
        // catalogue seeder.
        var existing = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.NormalizedName)
            .ToListAsync(cancellationToken);

        var have = existing.ToHashSet(StringComparer.Ordinal);
        var added = new List<Role>();

        foreach (var (name, description, keys) in Definitions())
        {
            if (have.Contains(name.ToUpperInvariant()))
            {
                continue;
            }

            var role = Role.CreateSystem(name, description, keys);
            role.TenantId = tenantId;
            db.Roles.Add(role);
            added.Add(role);
        }

        return added;
    }

    /// <summary>
    /// The seeded roles and their privilege sets.
    /// <para>
    /// The Department Head and Analyst grants are an explicit per-module table
    /// rather than a rule, because they were derived endpoint-by-endpoint from the
    /// fixed-tier gates this module replaces: for every permission-gated endpoint,
    /// the seeded holder set reproduces the old role list. Where the eight-action
    /// granularity cannot split two old audiences exactly, the table errs on the
    /// side documented in the module's validation record (e.g. a Department Head
    /// may now archive an interested party because closing context issues and
    /// archiving parties share <c>org-context.void</c>).
    /// </para>
    /// </summary>
    private static IEnumerable<(string Name, string Description, IReadOnlyList<string> Keys)> Definitions()
    {
        yield return (
            TenantAdministrator,
            "Full access to every module, including user accounts, organisation structure and privileges.",
            PermissionCatalog.AllKeys.ToArray());

        yield return (
            QualityManager,
            "Runs the quality system: creates, approves and signs quality records across all modules, and "
            + "maintains the organisation structure and lists. Reads privilege configuration but does not "
            + "change accounts or privileges.",
            KeysWhere((module, action) => module.Key switch
            {
                // Parity with the fixed tiers: user administration and tenant
                // settings were tenant-admin only.
                PermissionCatalog.Users or PermissionCatalog.TenantSettings => false,
                PermissionCatalog.RolesPrivileges => action is PermissionAction.View,
                // Branch/department/test/list upkeep was QM work; deactivating
                // org units (Manage) stays tenant-admin.
                PermissionCatalog.Organization => action is not PermissionAction.Manage,
                _ => true,
            }));

        yield return (
            DepartmentHead,
            "Runs a department's day-to-day quality work: assigns training and competence, registers "
            + "standards, monitoring and studies, reviews documents and handles complaints and feedback.",
            Grants(
                (PermissionCatalog.Nonconformances, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Complaints, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Feedback, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Void, PermissionAction.Export]),
                (PermissionCatalog.Audits, [PermissionAction.View, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.QualityObjectives, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.ChangeControl, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.ManagementReviews, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.Documents, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Approve, PermissionAction.Export]),
                (PermissionCatalog.Records, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Risks, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Conflicts, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.OrgContext, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Void, PermissionAction.Export]),
                (PermissionCatalog.Equipment, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.ReferenceStandards, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.MonitoringPoints, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Suppliers, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Competencies, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Training, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.TestAuthorizations, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.AnalyticalQuality, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.ProficiencyTesting, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Tasks, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit]),
                (PermissionCatalog.Notifications, [PermissionAction.View]),
                (PermissionCatalog.Reports, [PermissionAction.View, PermissionAction.Export])));

        yield return (
            Analyst,
            "Performs and records laboratory work: raises and edits records in the modules they work in, "
            + "without approval, signing or configuration rights.",
            Grants(
                (PermissionCatalog.Nonconformances, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Complaints, [PermissionAction.View, PermissionAction.Create, PermissionAction.Export]),
                (PermissionCatalog.Feedback, [PermissionAction.View, PermissionAction.Create, PermissionAction.Export]),
                (PermissionCatalog.Audits, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.QualityObjectives, [PermissionAction.View, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.ChangeControl, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.ManagementReviews, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.Documents, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Records, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.Risks, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Conflicts, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.OrgContext, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.Equipment, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.ReferenceStandards, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.MonitoringPoints, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.Suppliers, [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.Competencies, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.Training, [PermissionAction.View, PermissionAction.Edit, PermissionAction.Export]),
                (PermissionCatalog.TestAuthorizations, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.AnalyticalQuality, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.ProficiencyTesting, [PermissionAction.View, PermissionAction.Export]),
                (PermissionCatalog.Tasks, [PermissionAction.View, PermissionAction.Edit]),
                (PermissionCatalog.Notifications, [PermissionAction.View]),
                (PermissionCatalog.Reports, [PermissionAction.View, PermissionAction.Export])));

        yield return (
            ExternalAuditor,
            "Read-only access to the quality record for external audit, including the audit trail and "
            + "signature manifest. Cannot create, change, approve or sign anything, and does not see "
            + "administration.",
            KeysWhere((module, action) => module.Key switch
            {
                // Not part of the auditable quality record surface the fixed
                // tier could reach.
                PermissionCatalog.QualityPolicy or PermissionCatalog.AccessReviews => false,
                // Review packs were exportable by QM/admin only.
                PermissionCatalog.ManagementReviews => action is PermissionAction.View,
                PermissionCatalog.Tasks or PermissionCatalog.Notifications => action is PermissionAction.View,
                _ => !IsAdministration(module) && ReadActions.Contains(action),
            }));
    }

    private static bool IsAdministration(PermissionModule module) =>
        module.Group == PermissionCatalog.GroupAdministration
        || module.Key is PermissionCatalog.Users;

    private static string[] KeysWhere(Func<PermissionModule, PermissionAction, bool> predicate) =>
        PermissionCatalog.Modules
            .SelectMany(m => m.Actions.Where(a => predicate(m, a)).Select(a => PermissionCatalog.Key(m.Key, a)))
            .ToArray();

    private static string[] Grants(params (string Module, PermissionAction[] Actions)[] table) =>
        table
            .SelectMany(row => row.Actions.Select(a => PermissionCatalog.Key(row.Module, a)))
            .ToArray();
}
