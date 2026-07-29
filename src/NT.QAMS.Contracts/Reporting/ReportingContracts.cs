namespace NT.QAMS.Contracts.Reporting;

/// <summary>
/// Live dashboard KPIs, each computed from real operational rows at request
/// time. <paramref name="ComputedAtUtc"/> is the freshness stamp the UI must
/// display (per the reporting architecture: every KPI declares its source and
/// freshness).
/// </summary>
public sealed record DashboardKpisDto(
    int OpenNcs,
    int OverdueCapaActions,
    int OpenComplaints,
    int AuditsInProgress,
    int EquipmentOutOfService,
    int EquipmentNeedsCalibration,
    int HighResidualRisks,
    int OverdueTasks,
    int PtUnsatisfactory,
    int PendingTrainingAssignments,
    int SuspendedSuppliers,
    int PublishedDocuments,
    DateTimeOffset ComputedAtUtc,
    DashboardKpiTotalsDto Totals);

/// <summary>
/// The population each KPI above is a subset of, so a count can be shown as a
/// proportion ("5 of 17") instead of a bare number. Every field is a real row
/// count for the tenant — no derived or assumed whole. A KPI is only ever
/// metered against the population it is genuinely drawn from: equipment out of
/// service and awaiting calibration share <see cref="EquipmentItems"/>, and the
/// remaining KPIs each pair with their own entity total.
/// </summary>
public sealed record DashboardKpiTotalsDto(
    int Nonconformances,
    int CapaActions,
    int Complaints,
    int Audits,
    int EquipmentItems,
    int Risks,
    int WorkTasks,
    int PtEnrollments,
    int TrainingAssignments,
    int Suppliers,
    int Documents);

/// <summary>One day of real KPI history from read.kpi_snapshot.</summary>
public sealed record KpiHistoryPointDto(
    DateOnly Date, int OpenNcs, int OverdueCapaActions, int OpenComplaints,
    int EquipmentOutOfService, int HighResidualRisks, int OverdueTasks);

/// <summary>Nonconformance count per source type, descending (Pareto).</summary>
public sealed record NcParetoBucketDto(string SourceType, int Count);

/// <summary>Work-task SLA compliance derived from due dates vs completion stamps.</summary>
public sealed record SlaComplianceDto(
    int CompletedTotal, int CompletedOnTime, decimal OnTimePercent,
    int OpenTotal, int OpenOverdue, DateTimeOffset ComputedAtUtc);
