using Microsoft.EntityFrameworkCore;
using NT.QAMS.Contracts.Common;

namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// API-004: normalized paging input. Out-of-range client values clamp to the
/// window instead of erroring — a hostile pageSize can never turn into an
/// unbounded query.
/// </summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public int Skip => (Page - 1) * PageSize;

    public static PageRequest Normalized(int page, int pageSize) => new(
        Math.Max(1, page),
        Math.Clamp(pageSize, 1, MaxPageSize));
}

/// <summary>Terminal operator turning an ORDERED projection into the envelope.</summary>
public static class PagedQuery
{
    public static async Task<PagedResponse<T>> ToPagedAsync<T>(
        this IQueryable<T> query, PageRequest page, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, total, page.Page, page.PageSize);
    }
}
