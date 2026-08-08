using NT.QAMS.Contracts.Common;
using NT.QAMS.Contracts.Reporting;

namespace NT.QAMS.Application.Abstractions;

/// <summary>One tabular section of an export (a register, a decision list, a KPI block…).</summary>
public sealed record ExportTable(
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// A complete export: title, provenance stamp (who generated it, when, for
/// which tenant — printed on every copy per 21 CFR Part 11 §11.10(b)), and
/// one or more tables. XLSX renders one worksheet per table; PDF renders
/// sections with a stamped footer on every page.
/// </summary>
public sealed record ExportPack(
    string Title,
    string TenantName,
    string GeneratedBy,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ExportTable> Tables);

/// <summary>One statistic tile of a page export — mirrors the on-screen stat cards.</summary>
public sealed record ExportStat(string Label, string Value, string? Tone);

/// <summary>
/// A register-page export: the page title, the human-readable filter line that
/// was in force, the statistic tiles as shown, and the (filtered) grid. The
/// provenance stamp says whose view of the data this was — the export is a copy
/// of what that user could see, not an independent report.
/// </summary>
public sealed record PageExportPack(
    string Title,
    string TenantName,
    string GeneratedBy,
    DateTimeOffset GeneratedAtUtc,
    string? FiltersSummary,
    IReadOnlyList<ExportStat> Stats,
    ExportTable Table);

/// <summary>
/// A complete Quality Analytics report: the full analytics computation (composite
/// Quality Health Score, its weighted components, and every sub-system section the
/// caller may see) plus the provenance stamp and the filter line in force. Unlike
/// the flat <see cref="PageExportPack"/>, this carries the structured
/// <see cref="QualityAnalyticsDto"/> so the renderer can draw section-specific
/// visuals — the score gauge, per-category progress bars, the Pareto bars and the
/// 5×5 risk heat-matrix — rather than a single grid.
/// </summary>
public sealed record QualityAnalyticsReportPack(
    string TenantName,
    string GeneratedBy,
    DateTimeOffset GeneratedAtUtc,
    string? FiltersSummary,
    QualityAnalyticsDto Analytics);

/// <summary>
/// Renders accurate and complete copies of electronic records (Part 11
/// §11.10(b)): real XLSX and paginated PDF — never HTML masquerading as
/// either. Implementations must stamp provenance on every output.
/// </summary>
public interface IExportService
{
    byte[] ToXlsx(ExportPack pack);
    byte[] ToPdf(ExportPack pack);

    /// <summary>Branded register-page copy: header band, stat tiles, grid.</summary>
    byte[] ToPageXlsx(PageExportPack pack);

    /// <summary>Branded register-page copy: header band, stat tiles, grid.</summary>
    byte[] ToPagePdf(PageExportPack pack);

    /// <summary>
    /// Branded, comprehensive Quality Analytics report: cover band, the composite
    /// Quality Health Score with a gauge and weighted per-category progress bars,
    /// and one section per sub-system (KPI tiles, Pareto bars, risk heat-matrix).
    /// </summary>
    byte[] ToQualityAnalyticsReportPdf(QualityAnalyticsReportPack pack);

    /// <summary>The same report as a real workbook: a health-score summary sheet plus one sheet per sub-system.</summary>
    byte[] ToQualityAnalyticsReportXlsx(QualityAnalyticsReportPack pack);

    /// <summary>
    /// A complete, professional User Manual PDF: cover with a topics-per-section
    /// overview chart, a table of contents, and one card per topic (summary,
    /// numbered progress bar over the workflow steps, and the "how to use" list).
    /// </summary>
    byte[] ToManualPdf(ManualExportPack pack);
}

/// <summary>
/// The assembled User Manual to render, localized to one language, plus provenance.
/// The content is the SPA's help catalogue passed through <see cref="ManualGroupDto"/>;
/// the renderer lays it out but does not author it.
/// </summary>
public sealed record ManualExportPack(
    string TenantName,
    string GeneratedBy,
    DateTimeOffset GeneratedAtUtc,
    string Language,
    IReadOnlyList<ManualGroupDto> Groups);
