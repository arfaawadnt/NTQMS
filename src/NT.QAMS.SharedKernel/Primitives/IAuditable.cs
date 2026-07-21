namespace NT.QAMS.SharedKernel.Primitives;

/// <summary>
/// Audit-stamp fields set exclusively by the persistence interceptor
/// (database-truth timestamps come from IClock, actor from ICurrentUser).
/// Domain code never writes these.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? ModifiedAtUtc { get; set; }
    string? ModifiedBy { get; set; }
}
