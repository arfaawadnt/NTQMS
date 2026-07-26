namespace NT.QAMS.WebApi.Authorization;

/// <summary>
/// The single source of truth for role identifiers used in <c>[Authorize(Roles = …)]</c>
/// (F-16: role identifiers were scattered as magic strings across controllers). The
/// names match the <see cref="NT.QAMS.Domain.IdentityAccess.UserRole"/> enum exactly —
/// that is what the JWT role claim carries — so a rename in one place is a compile
/// error here rather than a silent authorization gap. Groups are comma-joined string
/// constants (attribute arguments must be compile-time constants); order is irrelevant
/// to ASP.NET Core, which treats the list as "any of".
/// </summary>
public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string QualityManager = "QualityManager";
    public const string DepartmentHead = "DepartmentHead";
    public const string Analyst = "Analyst";
    public const string ExternalAuditor = "ExternalAuditor";

    /// <summary>Tenant administrators only.</summary>
    public const string TenantAdminOnly = TenantAdmin;

    /// <summary>Quality manager or tenant administrator — the common quality-approval group.</summary>
    public const string QmOrAdmin = QualityManager + "," + TenantAdmin;

    /// <summary>Quality-ledger readers: quality manager, tenant administrator, or an external auditor.</summary>
    public const string QmAdminAuditor = QualityManager + "," + TenantAdmin + "," + ExternalAuditor;

    /// <summary>Approvers that include a department head (e.g. archiving, competency sign-off).</summary>
    public const string QmDeptAdmin = QualityManager + "," + DepartmentHead + "," + TenantAdmin;
}
