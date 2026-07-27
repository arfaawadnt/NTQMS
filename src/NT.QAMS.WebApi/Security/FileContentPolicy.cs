namespace NT.QAMS.WebApi.Security;

/// <summary>
/// API-005: upload allow-list with content sniffing. A file is accepted only
/// when its extension is on the evidence allow-list AND its leading bytes
/// match that type's signature — a renamed executable fails the sniff no
/// matter what the client declares. The client's Content-Type is never
/// trusted or stored: the canonical type for the extension is, so a stored
/// file can never replay with an attacker-chosen media type.
/// </summary>
public static class FileContentPolicy
{
    /// <summary>How many leading bytes <see cref="Inspect"/> needs (text scan window).</summary>
    public const int HeaderLength = 512;

    private sealed record AllowedType(string CanonicalContentType, byte[][] Signatures, bool IsText);

    private static readonly byte[][] ZipSignature = [[0x50, 0x4B, 0x03, 0x04]];
    private static readonly byte[][] OleSignature = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]];

    /// <summary>The laboratory-evidence allow-list, keyed by lower-case extension.</summary>
    private static readonly Dictionary<string, AllowedType> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new("application/pdf", [[0x25, 0x50, 0x44, 0x46]], IsText: false),
        [".png"] = new("image/png", [[0x89, 0x50, 0x4E, 0x47]], IsText: false),
        [".jpg"] = new("image/jpeg", [[0xFF, 0xD8, 0xFF]], IsText: false),
        [".jpeg"] = new("image/jpeg", [[0xFF, 0xD8, 0xFF]], IsText: false),
        [".docx"] = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ZipSignature, IsText: false),
        [".xlsx"] = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ZipSignature, IsText: false),
        [".doc"] = new("application/msword", OleSignature, IsText: false),
        [".xls"] = new("application/vnd.ms-excel", OleSignature, IsText: false),
        [".csv"] = new("text/csv", [], IsText: true),
        [".txt"] = new("text/plain", [], IsText: true),
    };

    /// <summary>
    /// Validates <paramref name="fileName"/>'s extension against the
    /// allow-list and <paramref name="header"/> against that type's
    /// signature. Returns the canonical content type to STORE on success;
    /// a human-readable refusal otherwise.
    /// </summary>
    public static (string? CanonicalContentType, string? Refusal) Inspect(
        string fileName, ReadOnlySpan<byte> header)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !Allowed.TryGetValue(extension, out var type))
        {
            return (null, $"File type '{extension}' is not on the evidence allow-list " +
                          $"({string.Join(", ", Allowed.Keys.Order())}).");
        }

        if (type.IsText)
        {
            // Text formats have no signature — refuse binary masquerading as text.
            foreach (var b in header)
            {
                if (b == 0)
                {
                    return (null, $"The content is binary, not the {extension} text format it claims.");
                }
            }

            return (type.CanonicalContentType, null);
        }

        foreach (var signature in type.Signatures)
        {
            if (header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature))
            {
                return (type.CanonicalContentType, null);
            }
        }

        return (null, $"The content does not match the {extension} file signature.");
    }
}
