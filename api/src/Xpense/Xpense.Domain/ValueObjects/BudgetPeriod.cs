namespace Xpense.Domain.ValueObjects;

public sealed record BudgetPeriod(DateTime From, DateTime ToExclusive, string Name)
{
    public bool Contains(DateTime instant) => instant >= From && instant < ToExclusive;

    public bool Overlaps(DateTime from, DateTime toExclusive) => From < toExclusive && ToExclusive > from;
}
