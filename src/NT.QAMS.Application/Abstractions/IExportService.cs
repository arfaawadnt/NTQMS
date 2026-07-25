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

/// <summary>
/// Renders accurate and complete copies of electronic records (Part 11
/// §11.10(b)): real XLSX and paginated PDF — never HTML masquerading as
/// either. Implementations must stamp provenance on every output.
/// </summary>
public interface IExportService
{
    byte[] ToXlsx(ExportPack pack);
    byte[] ToPdf(ExportPack pack);
}
