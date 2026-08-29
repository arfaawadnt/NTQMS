namespace NT.QAMS.Infrastructure.Persistence;

/// <summary>
/// The application-wide PostgreSQL advisory-lock key space. Advisory locks are
/// per-database, so keys only need to be unique within NT.QMS: every key is
/// ASCII "NTQMS" (0x4E54514D53) followed by a one-byte discriminator. Keeping
/// them in one place guarantees two features can never collide on a key.
/// </summary>
public static class AdvisoryLockKeys
{
    /// <summary>
    /// Session-scoped singleton held for the process lifetime by
    /// <see cref="Jobs.SingleReplicaGuardService"/> — contention means a second
    /// replica shares this database (ADR-0001).
    /// </summary>
    public const long SingleReplicaSentinel = 0x4E54514D_5301;

    /// <summary>
    /// Transaction-scoped leader election for the hourly compliance sweep —
    /// exactly one instance executes a sweep round at a time.
    /// </summary>
    public const long ComplianceSweep = 0x4E54514D_5302;

    /// <summary>
    /// Transaction-scoped leader election for the KPI snapshot projection —
    /// exactly one instance upserts a snapshot round at a time.
    /// </summary>
    public const long KpiSnapshot = 0x4E54514D_5303;

    /// <summary>
    /// Transaction-scoped leader election for the ADT payload-retention purge
    /// (M-12 / ADR-0011) — exactly one instance purges a round at a time.
    /// </summary>
    public const long IntegrationPayloadRetention = 0x4E54514D_5304;
}
