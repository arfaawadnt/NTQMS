namespace NT.QAMS.SharedKernel.Primitives;

/// <summary>
/// A domain-rule violation with a machine-readable code (e.g. "SOD-CAPA-001",
/// "TENANT-002"). Surfaced by the API as an RFC 7807 problem with that code.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Guarded state-machine transition rejected.</summary>
public sealed class InvalidStateTransitionException(string code, string message)
    : DomainException(code, message);
