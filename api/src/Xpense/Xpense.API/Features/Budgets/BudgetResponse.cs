using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xpense.API.Contracts;
using Xpense.Domain.Entities;
using Xpense.Domain.ValueObjects;

namespace Xpense.API.Features.Budgets;

/// <summary>
/// Shared by all five budget slices: a budget looks the same however you fetched it.
/// <para>
/// <c>StartsOn</c> and <c>EndsOn</c> are dates rather than timestamps, so they cross the wire as
/// yyyy-MM-dd. A budget starts on a day, not at an instant, and sending midnight UTC would invite a
/// client to render it in local time and show the day before.
/// </para>
/// </summary>
public sealed record BudgetResponse(
    int Id,
    CategoryResponse Category,
    MoneyResponse Amount,
    string Recurrence,
    string StartsOn,
    string? EndsOn,
    BudgetPeriodResponse? Period,
    string CreatedAt,
    string? UpdatedAt)
{
    public static BudgetResponse Of(Budget budget, BudgetPeriodResponse? period) => new(
        budget.Id,
        CategoryResponse.Of(budget.Category!),
        MoneyResponse.Of(budget.Amount),
        budget.Recurrence.ToString(),
        Day(budget.StartsOn),
        budget.EndsOn is null ? null : Day(budget.EndsOn.Value),
        period,
        Timestamps.Iso(budget.CreatedAt),
        Timestamps.Iso(budget.UpdatedAt));

    private static string Day(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// What one budget measured over one of its periods. Null on a budget that measures nothing at the
/// moment asked about -- before it starts, after it ends, or a one-off outside its window.
/// <para>
/// <c>Uncounted</c> is spending on this budget's category inside this period that is in a different
/// currency, and so counts toward nothing here. It is reported rather than dropped: Xpense never
/// converts, and money that silently vanishes from a report is the failure this makes visible.
/// </para>
/// </summary>
public sealed record BudgetPeriodResponse(
    string Name,
    string From,
    string ToExclusive,
    MoneyResponse Spent,
    MoneyResponse Remaining,
    bool Exceeded,
    MoneyResponse[] Uncounted)
{
    public static BudgetPeriodResponse Of(
        BudgetPeriod period,
        Money limit,
        Money spent,
        IEnumerable<Money> uncounted) => new(
        period.Name,
        Timestamps.Iso(period.From),
        Timestamps.Iso(period.ToExclusive),
        MoneyResponse.Of(spent),
        MoneyResponse.Of(limit - spent),
        spent > limit,
        uncounted.Select(MoneyResponse.Of).ToArray());
}
