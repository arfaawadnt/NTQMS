using System.Security.Cryptography;
using System.Text;

namespace NT.QAMS.Application.Compliance;

/// <summary>
/// Computes the deterministic content hash that binds a 21 CFR Part 11 electronic
/// signature to the exact record state it attests to (§11.70 — the signature/record
/// linking requirement). Publishing a controlled document hashes the approved file's
/// bytes; regulated records that have no file — nonconformances, analytical studies,
/// quality policies, reviews — instead hash a canonical projection of the fields the
/// signature is meant to cover, produced here.
/// <para>
/// The projection is order-sensitive and delimiter-escaped, so the same record state
/// always yields the same hash and no two field layouts can collide. Present values
/// carry an <c>s:</c> tag and null carries an <c>n:</c> tag, so a null can never
/// collide with any string value — including one that looks like the null marker.
/// Callers list the covered fields explicitly, so a signature can never be silently
/// rebound to a different field set.
/// </para>
/// </summary>
public static class SignatureContentHash
{
    /// <summary>
    /// SHA-256 (lower-case hex) over the ordered <paramref name="fields"/>. Each pair
    /// is rendered as <c>name=tag:value</c> on its own line; both sides are escaped so
    /// a value containing a delimiter cannot forge a different field layout.
    /// </summary>
    public static string Compute(params (string Name, string? Value)[] fields)
    {
        var canonical = new StringBuilder();
        foreach (var (name, value) in fields)
        {
            canonical
                .Append(Escape(name))
                .Append('=')
                .Append(value is null ? "n:" : "s:" + Escape(value))
                .Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("=", "\\=", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal);
}
