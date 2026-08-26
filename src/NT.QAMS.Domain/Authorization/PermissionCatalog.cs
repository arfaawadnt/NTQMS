namespace NT.QAMS.Domain.Authorization;

/// <summary>
/// What a permission lets an actor do to a module. A deliberately small, closed
/// set: the matrix an administrator configures is <c>module × action</c>, not one
/// switch per endpoint, so the privilege screen stays comprehensible and a new
/// endpoint inherits an existing meaning instead of silently being ungoverned.
/// </summary>
public enum PermissionAction
{
    /// <summary>Read the module's records and reports.</summary>
    View = 0,

    /// <summary>Create new records in the module.</summary>
    Create = 1,

    /// <summary>Modify existing records that are still editable.</summary>
    Edit = 2,

    /// <summary>Approve, publish, verify or otherwise advance a record through a controlled gate.</summary>
    Approve = 3,

    /// <summary>Void, reject, close-out or release a record (state-destroying transitions).</summary>
    Void = 4,

    /// <summary>Apply an electronic signature on the module's regulated records (21 CFR Part 11).</summary>
    Sign = 5,

    /// <summary>Export the module's data out of the system.</summary>
    Export = 6,

    /// <summary>Administer the module's configuration (not its day-to-day records).</summary>
    Manage = 7,
}

/// <summary>
/// One configurable module in the privilege matrix, as a user of the privilege
/// screen understands it — grouped the way the navigation is grouped, not the way
/// the code is packaged.
/// </summary>
/// <param name="Key">Stable identifier used in permission keys; never localised, never renamed.</param>
/// <param name="Group">Navigation group the module belongs to, for rendering the matrix.</param>
/// <param name="NameKey">i18n key for the module's display name.</param>
/// <param name="Actions">The actions that are meaningful for this module.</param>
public sealed record PermissionModule(
    string Key,
    string Group,
    string NameKey,
    IReadOnlyList<PermissionAction> Actions);

/// <summary>
/// The single source of truth for every permission the system recognises.
/// <para>
/// Permissions are <b>code-defined, not data-defined</b>: an administrator grants
/// and revokes them, but cannot invent one, because each key is wired to a real
/// code path. A key that no longer exists in this catalogue is rejected when a
/// role is saved, so a renamed module can never leave a dangling grant that looks
/// like an active privilege.
/// </para>
/// <para>
/// Keys are <c>{module}.{action}</c> in lower case (e.g. <c>nc.approve</c>) and are
/// persisted verbatim, so they must be treated as a stable contract.
/// </para>
/// </summary>
public static class PermissionCatalog
{
    // ── Groups (match the shell navigation so the matrix reads like the app) ──
    public const string GroupQuality = "quality";
    public const string GroupDocuments = "documents";
    public const string GroupRisk = "risk";
    public const string GroupResources = "resources";
    public const string GroupPeople = "people";
    public const string GroupAnalytical = "analytical";
    public const string GroupOperations = "operations";
    public const string GroupAdministration = "administration";

    // ── Module keys ───────────────────────────────────────────────────────────
    public const string Nonconformances = "nc";
    public const string Incidents = "incidents";
    public const string Indicators = "indicators";
    public const string PatientSafety = "patient-safety";
    public const string InfectionControl = "infection-control";
    public const string MortalityReview = "mortality-review";
    public const string Standards = "standards";
    public const string Complaints = "complaints";
    public const string Feedback = "feedback";
    public const string Surveys = "surveys";
    public const string Audits = "audits";
    public const string QualityObjectives = "objectives";
    public const string ChangeControl = "changes";
    public const string ManagementReviews = "reviews";
    public const string Committees = "committees";
    public const string Documents = "documents";
    public const string QualityPolicy = "quality-policy";
    public const string Records = "records";
    public const string Risks = "risks";
    public const string Compliance = "compliance";
    public const string Conflicts = "conflicts";
    public const string AccessReviews = "access-reviews";
    public const string Equipment = "equipment";
    public const string ReferenceStandards = "reference-standards";
    public const string MonitoringPoints = "monitoring-points";
    public const string Suppliers = "suppliers";
    public const string Competencies = "competencies";
    public const string Credentialing = "credentialing";
    public const string Training = "training";
    public const string TestAuthorizations = "test-authorizations";
    public const string Users = "users";
    public const string AnalyticalQuality = "analytical-quality";
    public const string ProficiencyTesting = "proficiency-testing";
    public const string Tasks = "tasks";
    public const string Notifications = "notifications";
    public const string Integration = "integration";
    public const string EnvironmentOfCare = "environment-of-care";
    public const string Reports = "reports";
    public const string Organization = "organization";
    public const string TenantSettings = "tenant-settings";
    public const string RolesPrivileges = "roles";
    public const string OrgContext = "org-context";

    private static readonly PermissionAction[] FullRecordLifecycle =
    [
        PermissionAction.View, PermissionAction.Create, PermissionAction.Edit,
        PermissionAction.Approve, PermissionAction.Void, PermissionAction.Export,
    ];

    private static readonly PermissionAction[] SignedRecordLifecycle =
    [
        PermissionAction.View, PermissionAction.Create, PermissionAction.Edit,
        PermissionAction.Approve, PermissionAction.Void, PermissionAction.Sign,
        PermissionAction.Export,
    ];

    private static readonly PermissionAction[] ReadOnlyModule = [PermissionAction.View, PermissionAction.Export];

    private static readonly PermissionAction[] ConfigurationModule =
        [PermissionAction.View, PermissionAction.Manage];

    /// <summary>
    /// Every configurable module, in the order the privilege matrix renders them.
    /// This is what "all modules on the system" means concretely — adding a module
    /// here is what makes it governable.
    /// </summary>
    public static readonly IReadOnlyList<PermissionModule> Modules =
    [
        // ── Quality & improvement ────────────────────────────────────────────
        new(Nonconformances, GroupQuality, "perm.mod.nc", SignedRecordLifecycle),
        new(Incidents, GroupQuality, "perm.mod.incidents", SignedRecordLifecycle),
        new(Indicators, GroupQuality, "perm.mod.indicators", FullRecordLifecycle),
        new(PatientSafety, GroupQuality, "perm.mod.patientSafety", FullRecordLifecycle),
        new(InfectionControl, GroupQuality, "perm.mod.infectionControl", FullRecordLifecycle),
        new(MortalityReview, GroupQuality, "perm.mod.mortalityReview", FullRecordLifecycle),
        new(Complaints, GroupQuality, "perm.mod.complaints", FullRecordLifecycle),
        new(Feedback, GroupQuality, "perm.mod.feedback", FullRecordLifecycle),
        new(Surveys, GroupQuality, "perm.mod.surveys", FullRecordLifecycle),
        new(Audits, GroupQuality, "perm.mod.audits", SignedRecordLifecycle),
        new(QualityObjectives, GroupQuality, "perm.mod.objectives", FullRecordLifecycle),
        new(ChangeControl, GroupQuality, "perm.mod.changes", SignedRecordLifecycle),
        new(ManagementReviews, GroupQuality, "perm.mod.reviews", SignedRecordLifecycle),
        new(Committees, GroupQuality, "perm.mod.committees", FullRecordLifecycle),

        // ── Documents & records ──────────────────────────────────────────────
        new(Documents, GroupDocuments, "perm.mod.documents", SignedRecordLifecycle),
        new(QualityPolicy, GroupDocuments, "perm.mod.qualityPolicy", SignedRecordLifecycle),
        new(Records, GroupDocuments, "perm.mod.records", FullRecordLifecycle),

        // ── Risk & governance ────────────────────────────────────────────────
        new(Risks, GroupRisk, "perm.mod.risks", FullRecordLifecycle),
        new(Compliance, GroupRisk, "perm.mod.compliance",
            [PermissionAction.View, PermissionAction.Create, PermissionAction.Approve, PermissionAction.Sign, PermissionAction.Export]),
        new(Standards, GroupRisk, "perm.mod.standards", FullRecordLifecycle),
        new(Conflicts, GroupRisk, "perm.mod.conflicts", SignedRecordLifecycle),
        new(OrgContext, GroupRisk, "perm.mod.orgContext",
            [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Void, PermissionAction.Export]),
        new(AccessReviews, GroupRisk, "perm.mod.accessReviews", SignedRecordLifecycle),

        // ── Resources ────────────────────────────────────────────────────────
        new(Equipment, GroupResources, "perm.mod.equipment", FullRecordLifecycle),
        new(ReferenceStandards, GroupResources, "perm.mod.referenceStandards", FullRecordLifecycle),
        new(MonitoringPoints, GroupResources, "perm.mod.monitoringPoints", FullRecordLifecycle),
        new(Suppliers, GroupResources, "perm.mod.suppliers", SignedRecordLifecycle),

        // ── People & competence ──────────────────────────────────────────────
        new(Competencies, GroupPeople, "perm.mod.competencies", SignedRecordLifecycle),
        new(Credentialing, GroupPeople, "perm.mod.credentialing", FullRecordLifecycle),
        new(Training, GroupPeople, "perm.mod.training", FullRecordLifecycle),
        new(TestAuthorizations, GroupPeople, "perm.mod.testAuthorizations", SignedRecordLifecycle),
        new(Users, GroupPeople, "perm.mod.users", ConfigurationModule),

        // ── Analytical quality ───────────────────────────────────────────────
        new(AnalyticalQuality, GroupAnalytical, "perm.mod.analyticalQuality",
            [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Approve,
             PermissionAction.Void, PermissionAction.Sign, PermissionAction.Export, PermissionAction.Manage]),
        new(ProficiencyTesting, GroupAnalytical, "perm.mod.proficiencyTesting", SignedRecordLifecycle),

        // ── Operations ───────────────────────────────────────────────────────
        new(Tasks, GroupOperations, "perm.mod.tasks", [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Manage]),
        new(Notifications, GroupOperations, "perm.mod.notifications", [PermissionAction.View, PermissionAction.Manage]),
        new(Integration, GroupOperations, "perm.mod.integration",
            [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Manage]),
        new(EnvironmentOfCare, GroupOperations, "perm.mod.environmentOfCare", FullRecordLifecycle),
        // Reporting carries Manage in addition to the read-only pair: the composite
        // Quality Health Score is a governance figure, so tuning its category
        // weighting is a privileged act distinct from reading the analytics.
        new(Reports, GroupOperations, "perm.mod.reports",
            [PermissionAction.View, PermissionAction.Export, PermissionAction.Manage]),

        // ── Administration ───────────────────────────────────────────────────
        new(Organization, GroupAdministration, "perm.mod.organization",
            [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Manage]),
        new(TenantSettings, GroupAdministration, "perm.mod.tenantSettings", ConfigurationModule),
        new(RolesPrivileges, GroupAdministration, "perm.mod.roles", ConfigurationModule),
    ];

    /// <summary>Every valid permission key, for validation and for the matrix UI.</summary>
    public static readonly IReadOnlySet<string> AllKeys = Modules
        .SelectMany(m => m.Actions.Select(a => Key(m.Key, a)))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Builds the canonical <c>{module}.{action}</c> key.</summary>
    public static string Key(string moduleKey, PermissionAction action) =>
        $"{moduleKey}.{action.ToString().ToLowerInvariant()}";

    /// <summary>True when the key is one this build recognises.</summary>
    public static bool IsKnown(string permissionKey) => AllKeys.Contains(permissionKey);

    /// <summary>
    /// Managing roles and privileges. Held apart because it is the one permission
    /// that can lock every administrator out of the system, so it carries an
    /// extra invariant wherever roles are saved.
    /// </summary>
    public static string ManageRoles => Key(RolesPrivileges, PermissionAction.Manage);
}
