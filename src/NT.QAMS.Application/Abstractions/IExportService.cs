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
}
