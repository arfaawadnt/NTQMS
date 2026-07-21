using System.Text.RegularExpressions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Tenancy;

/// <summary>
/// The tenant's URL identifier (e.g. "amman-central-lab"). Immutable after
/// provisioning; globally unique; safe for hostnames and routes.
/// </summary>
public sealed partial class TenantSlug : ValueObject
{
    public const int MaxLength = 50;

    private TenantSlug(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static TenantSlug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("TENANT-001", "Tenant identifier is required.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength || !SlugPattern().IsMatch(normalized))
        {
            throw new DomainException(
                "TENANT-002",
                $"Tenant identifier must be 2-{MaxLength} chars of lowercase letters, digits and single hyphens, starting and ending with a letter or digit.");
        }

        return new TenantSlug(normalized);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex("^[a-z0-9](?:-?[a-z0-9]){1,49}$")]
    private static partial Regex SlugPattern();
}
