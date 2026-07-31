namespace NT.QAMS.SharedKernel.MultiTenancy;

/// <summary>
/// An aggregate that belongs to a tenant when it has one and to the platform
/// when it does not — user accounts are the canonical case (tenant users vs
/// platform administrators). Cross-cutting writers (outbox draining, audit
/// attribution) use this to stamp events with the owning tenant without
/// forcing tenant membership on the type.
/// <para>
/// Found by OQ-RP-09 (defect RP-D1): user-account events — role assignment,
/// scope changes, lockouts — were landing in the audit ledger with an empty
/// tenant id, invisible to the very tenant whose access control they record.
/// </para>
/// </summary>
public interface IOptionallyTenantScoped
{
    /// <summary>The owning tenant, or null for platform-level records.</summary>
    Guid? TenantId { get; }
}
