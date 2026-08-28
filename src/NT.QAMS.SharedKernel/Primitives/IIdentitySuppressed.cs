namespace NT.QAMS.SharedKernel.Primitives;

/// <summary>
/// A record whose ORIGINATING actor's identity must not be persisted — the
/// anonymous-reporting contract (HQMS M02, audit finding B-01). When
/// <see cref="IdentitySuppressed"/> is true, the audit stamp and the
/// field-change ledger attribute the record's CREATION to "anonymous" and store
/// no user id; every LATER transition is a named workflow actor and stays fully
/// attributed. The suppression is real: the system genuinely does not know the
/// originator, so preparer-based segregation-of-duties cannot apply to the
/// creation — a deliberate consequence of the promise, recorded on the aggregate.
/// </summary>
public interface IIdentitySuppressed
{
    /// <summary>True when this record's creation must carry no actor identity.</summary>
    bool IdentitySuppressed { get; }
}
