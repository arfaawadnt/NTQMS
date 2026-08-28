namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// The one boundary conversion from a request string to a domain enum (M-11).
/// <para>
/// <see cref="Enum.Parse{TEnum}(string, bool)"/> has two failure modes at a
/// public boundary: an unknown name throws (an unhandled 500), and a numeric
/// string silently produces an UNDEFINED value that travels into the domain
/// until a database CHECK kills the transaction. This helper closes both —
/// case-insensitive on names, and defined-values only. The thrown
/// <see cref="ArgumentException"/> is mapped to a 400 <c>REQ-001</c> problem
/// by <see cref="Middleware.DomainExceptionHandler"/>.
/// </para>
/// </summary>
public static class RequestEnum
{
    public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"'{value}' is not a valid {typeof(TEnum).Name}.");
    }
}
