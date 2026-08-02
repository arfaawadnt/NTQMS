namespace NT.QAMS.Contracts.Common;

/// <summary>One statistic tile of a page export, as shown on screen.</summary>
public sealed record PageExportStatDto(string Label, string Value, string? Tone);

/// <summary>
/// A register page asking to be rendered as a document. The payload is the
/// caller's <em>own view</em> of the data — title, the filter line in force,
/// the statistic tiles, and the filtered grid — so the export can never show a
/// caller more than their privileges already did. The server formats and
/// stamps; it does not re-query.
/// </summary>
public sealed record PageExportRequest(
    string Title,
    string? FiltersSummary,
    IReadOnlyList<PageExportStatDto> Stats,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);
