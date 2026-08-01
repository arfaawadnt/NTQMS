namespace NT.QAMS.Contracts.Reporting;

/// <summary>A labelled count, for the small category breakdowns (donuts and bars).</summary>
public sealed record CategoryCountDto(string Label, int Count);

/// <summary>One recent record row shown beneath a section, kept deliberately shallow.</summary>
public sealed record AnalyticsRowDto(string Reference, string Title, string? Detail, string Status);

// ── Section statistics ───────────────────────────────────────────────────────
// Each section reports its own population alongside the figure drawn from it, so
// the UI can state a proportion instead of a bare count — the same rule the
// dashboard KPI strip follows.

/// <summary>Document and SOP control (ISO 17025 §8.3, review-due horizon buckets).</summary>
public sealed record DocumentControlStatsDto(
    int TotalActive,
    int Current,
    decimal? PercentCurrent,
    int OverdueReviews,
    int DueWithin30,
    int Due31To60,
    int Due61To90,
    /// <summary>
    /// Read-and-understand receipts on file. This is a count of acknowledgements
    /// <em>recorded</em>, not of acknowledgements outstanding: the system records a
    /// receipt when it is given and has no roster of who was required to give one,
    /// so an "outstanding" figure would have no denominator behind it.
    /// </summary>
    int AcknowledgementsRecorded,
    IReadOnlyList<AnalyticsRowDto> UpcomingReviews);

/// <summary>Nonconformances and CAPA effectiveness (ISO 17025 §8.7).</summary>
public sealed record NcCapaStatsDto(
    int OpenNcs,
    int TotalNcs,
    int OverdueCapa,
    int TotalCapa,
    int CapaClosedOnTime,
    int CapaClosedTotal,
    /// <summary>
    /// Share of closed CAPA actions completed on or before their due date.
    /// Effectiveness is measured against the commitment, not as a closure
    /// duration: a CAPA action records when it was completed but not when it was
    /// raised, so an elapsed-days figure would have no start to measure from.
    /// Null until something has actually closed.
    /// </summary>
    decimal? CapaOnTimePercent,
    /// <summary>
    /// Share of all CAPA actions that are not overdue. This — not
    /// <see cref="CapaOnTimePercent"/> — is the category's contribution to the
    /// composite score, because it is defined as soon as any CAPA exists, whereas
    /// closure rate stays null until the first action closes and would silently
    /// drop the whole category out of the score.
    /// </summary>
    decimal? CapaOnSchedulePercent,
    IReadOnlyList<CategoryCountDto> ByStatus,
    IReadOnlyList<CategoryCountDto> BySource,
    IReadOnlyList<CategoryCountDto> ByDepartment,
    IReadOnlyList<AnalyticsRowDto> Active);

/// <summary>
/// Customer complaints (ISO 17025 §7.9). Grouped by intake <em>channel</em>: the
/// complaint record carries a channel, not a subject category, so a category
/// breakdown would have to be invented.
/// </summary>
public sealed record ComplaintsStatsDto(
    int Open,
    int Total,
    int ResolvedWithinSla,
    int ResolvedTotal,
    decimal? PercentWithinSla,
    decimal? AverageResolutionDays,
    IReadOnlyList<CategoryCountDto> ByChannel,
    IReadOnlyList<AnalyticsRowDto> Active);

/// <summary>Internal audit programme (ISO 17025 §8.8).</summary>
public sealed record AuditStatsDto(
    int Completed,
    int TotalPlanned,
    decimal? PlanCompletionPercent,
    int MajorFindings,
    int MinorFindings,
    int Observations,
    IReadOnlyList<AnalyticsRowDto> Recent);

/// <summary>Equipment and calibration (ISO 17025 §6.4).</summary>
public sealed record EquipmentStatsDto(
    int Total,
    int CalibrationCurrent,
    decimal? CalibrationCompliancePercent,
    int OutOfService,
    decimal? AvailabilityPercent,
    int OverdueCalibration,
    IReadOnlyList<CategoryCountDto> ByStatus,
    IReadOnlyList<AnalyticsRowDto> UpcomingCalibrations);

/// <summary>Personnel competency and training (ISO 17025 §6.2).</summary>
public sealed record CompetencyStatsDto(
    int Authorized,
    int Total,
    decimal? PercentCompetent,
    int ExpiringWithin90,
    int Revoked,
    int PendingTraining,
    IReadOnlyList<AnalyticsRowDto> Recent);

/// <summary>Proficiency testing and interlaboratory comparison (ISO 17025 §7.7).</summary>
public sealed record PtStatsDto(
    int Satisfactory,
    int Questionable,
    int Unsatisfactory,
    int Pending,
    int Total,
    decimal? SatisfactionRatePercent,
    IReadOnlyList<AnalyticsRowDto> Recent);

/// <summary>External providers (ISO 17025 §6.6).</summary>
public sealed record SupplierStatsDto(
    int Approved,
    int Total,
    decimal? ApprovedPercent,
    int Suspended,
    decimal? AverageEvaluationScore,
    IReadOnlyList<AnalyticsRowDto> Recent);

/// <summary>One cell of the 5×5 likelihood × impact matrix (the domain fixes the 1–5 scale).</summary>
public sealed record RiskMatrixCellDto(int Likelihood, int Impact, int Count);

/// <summary>Risk and opportunity (ISO 17025 §8.5).</summary>
public sealed record RiskStatsDto(
    int HighOrExtreme,
    int Total,
    int HighMitigated,
    decimal? HighMitigatedPercent,
    int OverdueTreatments,
    IReadOnlyList<RiskMatrixCellDto> Matrix,
    IReadOnlyList<AnalyticsRowDto> Top);

// ── Composite health score ───────────────────────────────────────────────────

/// <summary>
/// One category's contribution. <paramref name="AchievedScore"/> is null when the
/// category has no population to score — an empty register is reported as
/// "no data", never as zero, because zero would drag the composite down as though
/// the lab had failed at something it has not yet done.
/// </summary>
public sealed record QualityHealthComponentDto(
    string Category,
    int Weight,
    decimal? AchievedScore,
    bool Contributed,
    string? ExcludedReason);

/// <summary>
/// The composite Quality Health Score: a weighted mean of the category scores that
/// actually contributed. <paramref name="Score"/> is null when nothing contributed.
/// The component list is always returned in full so a reviewer can reproduce the
/// arithmetic — the figure is never presented without the basis for it.
/// </summary>
public sealed record QualityHealthScoreDto(
    decimal? Score,
    IReadOnlyList<QualityHealthComponentDto> Components,
    int ContributingCategories,
    int TotalCategories);

/// <summary>
/// What the returned figures were computed over. <paramref name="UnscopedSections"/>
/// names the sections a branch/department filter could not narrow, because those
/// records carry no organisational attribution — stating that is the difference
/// between a filter that works and one that appears to.
/// </summary>
public sealed record QualityAnalyticsScopeDto(
    Guid? BranchId,
    Guid? DepartmentId,
    bool FilterApplied,
    IReadOnlyList<string> UnscopedSections,
    IReadOnlyList<string> HiddenSections);

/// <summary>
/// The full analytics payload behind both the Quality Statistics view and the
/// ISO 17025 §8.9.2 management-review view — one computation serving both, so the
/// two framings can never disagree about a number.
///
/// A section is <c>null</c> when the caller lacks the underlying module's view
/// permission. It is omitted server-side rather than hidden client-side, so an
/// unprivileged caller never receives the figures at all, and the health score is
/// computed only from the components that caller can actually see.
/// </summary>
public sealed record QualityAnalyticsDto(
    QualityHealthScoreDto Health,
    DocumentControlStatsDto? DocumentControl,
    NcCapaStatsDto? NcCapa,
    ComplaintsStatsDto? Complaints,
    AuditStatsDto? Audits,
    EquipmentStatsDto? Equipment,
    CompetencyStatsDto? Competency,
    PtStatsDto? ProficiencyTesting,
    SupplierStatsDto? Suppliers,
    RiskStatsDto? Risk,
    QualityAnalyticsScopeDto Scope,
    DateTimeOffset ComputedAtUtc);

// ── Health-score weighting (configuration) ───────────────────────────────────

/// <summary>One configurable category weight.</summary>
public sealed record QualityHealthWeightDto(string Category, int Weight);

/// <summary>The tenant's current weighting, for the configuration screen.</summary>
public sealed record QualityHealthProfileDto(IReadOnlyList<QualityHealthWeightDto> Weights);

/// <summary>
/// Replaces the weighting. Every category must be supplied and a reason is
/// mandatory: altering the definition of a reported quality metric is a controlled
/// change, recorded in the audit trail with its justification.
/// </summary>
public sealed record UpdateQualityHealthWeightsRequest(
    IReadOnlyList<QualityHealthWeightDto> Weights,
    string Reason);
