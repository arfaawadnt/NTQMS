namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// The operator-supplied justification for the mutation being performed in the
/// current unit of work (21 CFR Part 11 §11.10(e) / ALCOA+ "reason for change").
/// Captured from the request (e.g. the <c>X-Change-Reason</c> header on a void)
/// and stamped onto the field-change ledger rows written in the same save.
/// Null when the change carries no explicit reason (routine create/update).
/// </summary>
public interface ICurrentChangeReason
{
    /// <summary>The trimmed reason for this unit of work, or null when none was supplied.</summary>
    string? Reason { get; }
}

/// <summary>Write side used by the request middleware / command handlers.</summary>
public interface ICurrentChangeReasonSetter
{
    /// <summary>Records the reason for the current unit of work; blank input clears it.</summary>
    void Set(string? reason);
}
