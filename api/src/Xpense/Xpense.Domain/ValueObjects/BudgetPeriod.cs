namespace Xpense.Domain.ValueObjects;

/// <summary>
/// The window of time a budget measures expenses in, and the name that window goes by.
/// <para>
/// Half-open on purpose: <paramref name="From"/> is inside the period and
/// <paramref name="ToExclusive"/> is not, so one period ends exactly where the next begins with no
/// gap and no overlap. An inclusive end would mean choosing a last instant, and every choice of
/// last instant is wrong for something -- 23:59:59 loses the final second, 23:59:59.9999999 is a
/// number nobody would guess.
/// </para>
/// </summary>
/// <param name="From">The first instant inside the period, in UTC.</param>
/// <param name="ToExclusive">The first instant after the period, in UTC.</param>
/// <param name="Name">
/// What this period is called: <c>2026-W32</c>, <c>2026-08</c>, <c>2026</c>, or a date range for a
/// budget that does not repeat.
/// </param>
public sealed record BudgetPeriod(DateTime From, DateTime ToExclusive, string Name)
{
    public bool Contains(DateTime instant) => instant >= From && instant < ToExclusive;

    /// <summary>Whether this period shares any time at all with the half-open window given.</summary>
    public bool Overlaps(DateTime from, DateTime toExclusive) => From < toExclusive && ToExclusive > from;
}
