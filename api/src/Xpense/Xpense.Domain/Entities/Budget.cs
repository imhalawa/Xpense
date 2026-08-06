using System.Globalization;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;

namespace Xpense.Domain.Entities;

public class Budget : BaseEntity
{
    public const int DefaultAlertThreshold = 75;

    public long AmountMinorUnits { get; set; }

    public Currency Currency { get; set; }

    public Money Amount => Money.OfMinorUnits(AmountMinorUnits, Currency);

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public Recurrence Recurrence { get; set; }

    public DateTime StartsOn { get; set; }

    public DateTime? EndsOn { get; set; }

    public int? AlertThresholdPercent { get; set; }

    public Money? AlertThreshold =>
        AlertThresholdPercent is null
            ? null
            : Money.OfMinorUnits(AmountMinorUnits * AlertThresholdPercent.Value / 100, Currency);

    private DateTime LifeToExclusive => EndsOn?.AddDays(1) ?? DateTime.MaxValue;

    public static Budget For(
        Category category,
        Money amount,
        Recurrence recurrence,
        DateTime startsOn,
        DateTime? endsOn,
        int? alertThresholdPercent = DefaultAlertThreshold)
    {
        if (amount.MinorUnits <= 0)
            throw new InvalidBudgetException("A budget amount must be positive.");

        var (from, to) = RequireValidWindow(recurrence, startsOn, endsOn);

        return new Budget
        {
            Category = category,
            AmountMinorUnits = amount.MinorUnits,
            Currency = amount.Currency,
            Recurrence = recurrence,
            StartsOn = from,
            EndsOn = to,
            AlertThresholdPercent = RequireValidThreshold(alertThresholdPercent),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Restate(
        Money amount,
        Recurrence recurrence,
        DateTime startsOn,
        DateTime? endsOn,
        int? alertThresholdPercent = DefaultAlertThreshold)
    {
        if (amount.MinorUnits <= 0)
            throw new InvalidBudgetException("A budget amount must be positive.");

        var (from, to) = RequireValidWindow(recurrence, startsOn, endsOn);
        var threshold = RequireValidThreshold(alertThresholdPercent);

        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Recurrence = recurrence;
        StartsOn = from;
        EndsOn = to;
        AlertThresholdPercent = threshold;
        Touch();
    }

    private static int? RequireValidThreshold(int? percent)
    {
        if (percent is not null && percent is < 1 or > 100)
            throw new InvalidBudgetException("An alert threshold must be between 1 and 100 percent.");

        return percent;
    }

    private static (DateTime From, DateTime? To) RequireValidWindow(
        Recurrence recurrence,
        DateTime startsOn,
        DateTime? endsOn)
    {
        var from = UtcDate(startsOn);
        var to = endsOn is null ? null : (DateTime?)UtcDate(endsOn.Value);

        if (recurrence == Recurrence.None && to is null)
            throw new InvalidBudgetException("A budget that does not repeat must state when it ends.");

        if (to is not null && to < from)
            throw new InvalidBudgetException("A budget cannot end before it starts.");

        return (from, to);
    }

    public BudgetPeriod? PeriodOn(DateTime instant)
    {
        if (Recurrence == Recurrence.None)
        {
            var only = OnlyPeriod();
            return only.Contains(instant) ? only : null;
        }

        var period = CalendarPeriodContaining(instant);

        return period.Overlaps(StartsOn, LifeToExclusive) ? period : null;
    }

    private BudgetPeriod OnlyPeriod() =>
        new(StartsOn, LifeToExclusive, $"{Day(StartsOn)}..{Day(EndsOn!.Value)}");

    private BudgetPeriod CalendarPeriodContaining(DateTime instant) => Recurrence switch
    {
        Recurrence.Weekly => Week(instant),
        Recurrence.Monthly => Month(instant),
        Recurrence.Yearly => Year(instant),
        _ => throw new InvalidBudgetException($"Unsupported recurrence {Recurrence}.")
    };

    private static BudgetPeriod Week(DateTime instant)
    {
        var weekYear = ISOWeek.GetYear(instant);
        var week = ISOWeek.GetWeekOfYear(instant);
        var monday = DateTime.SpecifyKind(ISOWeek.ToDateTime(weekYear, week, DayOfWeek.Monday), DateTimeKind.Utc);

        return new BudgetPeriod(monday, monday.AddDays(7), $"{weekYear:0000}-W{week:00}");
    }

    private static BudgetPeriod Month(DateTime instant)
    {
        var first = new DateTime(instant.Year, instant.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new BudgetPeriod(first, first.AddMonths(1), $"{instant.Year:0000}-{instant.Month:00}");
    }

    private static BudgetPeriod Year(DateTime instant)
    {
        var first = new DateTime(instant.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new BudgetPeriod(first, first.AddYears(1), $"{instant.Year:0000}");
    }

    private static DateTime UtcDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static string Day(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
