namespace NT.QAMS.SharedKernel.Primitives;

/// <summary>
/// The single implementation of windowed day accrual — the denominator of every
/// per-1,000 patient-day and device-day rate. One rule set, applied everywhere:
/// the span clips to the window on both edges, never counts beyond
/// <c>asOf</c> (a future-dated end must not add days that have not elapsed),
/// and any positive overlap counts as at least one day (a same-day stay or
/// device episode is one day, not zero).
/// </summary>
public static class WindowedDays
{
    /// <summary>Whole days the span [start, end ?? asOf] overlaps [from, asOf].</summary>
    public static int Clipped(DateTimeOffset start, DateTimeOffset? end, DateTimeOffset from, DateTimeOffset asOf)
    {
        var clippedStart = start > from ? start : from;
        var clippedEnd = end ?? asOf;
        if (clippedEnd > asOf) { clippedEnd = asOf; }
        if (clippedEnd <= clippedStart) { return 0; }
        var days = (int)Math.Floor((clippedEnd - clippedStart).TotalDays);
        return days < 1 ? 1 : days;
    }
}
