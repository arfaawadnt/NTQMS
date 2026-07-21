namespace NT.QAMS.SharedKernel.Localization;

/// <summary>
/// Trilingual text value object (shared kernel item 3 of 3).
/// English is the mandatory fallback anchor; Arabic and French are optional.
/// Persisted as three columns (indexable, constraint-checkable) per the
/// database architecture decision — not as a translation table.
/// </summary>
public sealed record LocalizedText
{
    private LocalizedText(string en, string? ar, string? fr)
    {
        En = en;
        Ar = ar;
        Fr = fr;
    }

    public string En { get; }
    public string? Ar { get; }
    public string? Fr { get; }

    public static LocalizedText Create(string en, string? ar = null, string? fr = null)
    {
        if (string.IsNullOrWhiteSpace(en))
        {
            throw new ArgumentException("English text is the mandatory fallback anchor.", nameof(en));
        }

        return new LocalizedText(en.Trim(), Normalize(ar), Normalize(fr));
    }

    public string For(string languageCode) => languageCode?.ToLowerInvariant() switch
    {
        "ar" => Ar ?? En,
        "fr" => Fr ?? En,
        _ => En,
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
