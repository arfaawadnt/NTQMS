using NT.QAMS.SharedKernel.MultiTenancy;

namespace NT.QAMS.Domain.Reporting;

/// <summary>
/// One daily KPI snapshot row per tenant (read model, `read` schema). This is
/// derived data — never a source of truth — written by the snapshot sweep from
/// real operational rows so that trend charts show genuine history (the
/// fabricated-PRNG trends of the prototype are banned; per the architecture,
/// KPIs are computed from real events or not shown at all). Idempotent upsert
/// per (tenant, date).
/// </summary>
public sealed class KpiSnapshot : ITenantScoped
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }
    public DateOnly Date { get; init; }
    public int OpenNcs { get; set; }
    public int OverdueCapaActions { get; set; }
    public int OpenComplaints { get; set; }
    public int AuditsInProgress { get; set; }
    public int EquipmentOutOfService { get; set; }
    public int HighResidualRisks { get; set; }
    public int OverdueTasks { get; set; }
    public int PtUnsatisfactory { get; set; }
}
