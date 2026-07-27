namespace NT.QAMS.Contracts.Common;

/// <summary>
/// API-004: the pagination envelope every list endpoint returns — no more
/// silent result caps. <see cref="Total"/> is the full filtered count;
/// <see cref="HasMore"/> tells the client whether another page exists.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public bool HasMore => (long)Page * PageSize < Total;
}
