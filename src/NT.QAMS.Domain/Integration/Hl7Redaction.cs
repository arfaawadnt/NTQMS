namespace NT.QAMS.Domain.Integration;

/// <summary>
/// Masks patient-identifying fields in a raw HL7 v2 payload before it is stored
/// (M-12 / ADR-0011). The PID segment carries the direct identifiers — medical
/// record number, name, date of birth, address, contact numbers, SSN — none of
/// which the interface record needs to keep: the canonical <c>PatientRef</c> and
/// <c>EncounterRef</c> are extracted into the patient-stay projection, and the
/// stored payload exists only for interface troubleshooting, where the segment
/// STRUCTURE matters and the identifiers do not.
/// <para>
/// Conservative by construction: it masks the value of the listed PID fields and
/// leaves every other segment and the field delimiters intact, so a masked
/// message still shows the message shape. It never throws on malformed input —
/// an unparseable line is returned unchanged rather than risk dropping the
/// record.
/// </para>
/// </summary>
public static class Hl7Redaction
{
    private const string Mask = "***";

    /// <summary>1-based PID field positions that carry direct patient identifiers.</summary>
    private static readonly int[] PidPhiFields = [2, 3, 4, 5, 6, 7, 11, 13, 14, 19, 20];

    public static string MaskPatientIdentifiers(string? rawPayload)
    {
        if (string.IsNullOrEmpty(rawPayload))
        {
            return rawPayload ?? string.Empty;
        }

        // HL7 v2 uses \r between segments; tolerate \n and \r\n too.
        var normalizedSep = rawPayload.Contains('\r') ? '\r' : '\n';
        var segments = rawPayload.Split(normalizedSep);

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (!segment.StartsWith("PID|", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = segment.Split('|');
            foreach (var pos in PidPhiFields)
            {
                if (pos < fields.Length && fields[pos].Length > 0)
                {
                    fields[pos] = Mask;
                }
            }

            segments[i] = string.Join('|', fields);
        }

        return string.Join(normalizedSep, segments);
    }
}
