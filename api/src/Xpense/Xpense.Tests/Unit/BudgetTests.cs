using FluentAssertions;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;
using Entities = Xpense.Domain.Entities;

namespace Xpense.Tests.Unit;

/// <summary>
/// Period arithmetic and the budget's own invariants. No persistence, so these are genuine unit
/// tests; what a budget actually measures against real transactions is covered by the integration
/// suite.
/// </summary>
[TestFixture]
public class BudgetTests
{
    // A Thursday, comfortably inside week 32 of 2026 and the month of August.
    private static readonly DateTime InAugust = new(2026, 8, 6, 14, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_monthly_budget_measures_the_calendar_month_holding_the_instant()
    {
        var period = Monthly(startsOn: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)).PeriodOn(InAugust);

        period.Should().NotBeNull();
        period!.Name.Should().Be("2026-08");
        period.From.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        period.ToExclusive.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// A period runs to the start of the next one and no further, so the two never both claim an
    /// instant and no instant falls between them.
    /// </summary>
    [Test]
    public void A_period_excludes_its_end_and_includes_its_start()
    {
        var period = Monthly(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)).PeriodOn(InAugust)!;

        period.Contains(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        period.Contains(new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc)).Should().BeTrue();
        period.Contains(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    /// <summary>
    /// The whole month, not the part after the budget began. Activity is decided by the period
    /// overlapping the budget's life rather than by the instant being inside it -- otherwise asking
    /// about the 3rd and asking about the 20th would give two different answers for one period.
    /// </summary>
    [Test]
    public void A_monthly_budget_starting_mid_month_still_measures_the_whole_month()
    {
        var budget = Monthly(startsOn: new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));

        var fromBefore = budget.PeriodOn(new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc));
        var fromAfter = budget.PeriodOn(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));

        fromBefore.Should().NotBeNull();
        fromBefore.Should().Be(fromAfter);
        fromBefore!.From.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void A_weekly_budget_measures_the_iso_week_monday_to_monday()
    {
        var period = Weekly(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).PeriodOn(InAugust);

        period.Should().NotBeNull();
        period!.Name.Should().Be("2026-W32");
        period.From.DayOfWeek.Should().Be(DayOfWeek.Monday);
        period.From.Should().Be(new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc));
        period.ToExclusive.Should().Be(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// The ISO week year is not the calendar year at the boundary: 1 January 2027 belongs to week 53
    /// of 2026. Naming it from the instant's year would produce 2027-W53, a week that does not exist.
    /// </summary>
    [Test]
    public void A_weekly_period_is_named_by_its_iso_week_year_not_the_calendar_year()
    {
        var period = Weekly(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .PeriodOn(new DateTime(2027, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        period!.Name.Should().Be("2026-W53");
    }

    [Test]
    public void A_yearly_budget_measures_the_calendar_year()
    {
        var period = Yearly(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).PeriodOn(InAugust);

        period!.Name.Should().Be("2026");
        period.From.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        period.ToExclusive.Should().Be(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void A_repeating_budget_measures_nothing_before_it_starts()
    {
        var budget = Monthly(startsOn: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        budget.PeriodOn(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc)).Should().BeNull();
    }

    [Test]
    public void A_repeating_budget_measures_nothing_after_it_ends()
    {
        var budget = Entities.Budget.For(
            Category(),
            Money.OfMinorUnits(30000),
            Recurrence.Monthly,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc));

        budget.PeriodOn(InAugust).Should().NotBeNull();
        budget.PeriodOn(new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)).Should().BeNull();
    }

    /// <summary>
    /// A one-off has one window, which is its own dates rather than any calendar period, and it is
    /// named by them. Its end date is inclusive, so the last day still counts.
    /// </summary>
    [Test]
    public void A_one_off_budget_measures_its_own_window_and_nothing_outside_it()
    {
        var budget = Entities.Budget.For(
            Category(),
            Money.OfMinorUnits(20000),
            Recurrence.None,
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc));

        var period = budget.PeriodOn(new DateTime(2026, 8, 26, 23, 0, 0, DateTimeKind.Utc));

        period.Should().NotBeNull();
        period!.Name.Should().Be("2026-08-12..2026-08-26");
        period.ToExclusive.Should().Be(new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc));
        budget.PeriodOn(new DateTime(2026, 8, 11, 23, 0, 0, DateTimeKind.Utc)).Should().BeNull();
        budget.PeriodOn(new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc)).Should().BeNull();
    }

    [Test]
    public void A_budget_that_does_not_repeat_must_state_when_it_ends()
    {
        var act = () => Entities.Budget.For(
            Category(), Money.OfMinorUnits(20000), Recurrence.None, InAugust, endsOn: null);

        act.Should().Throw<InvalidBudgetException>().WithMessage("*must state when it ends*");
    }

    [Test]
    public void A_budget_cannot_end_before_it_starts()
    {
        var act = () => Entities.Budget.For(
            Category(),
            Money.OfMinorUnits(20000),
            Recurrence.Monthly,
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        act.Should().Throw<InvalidBudgetException>().WithMessage("*cannot end before it starts*");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void A_budget_amount_must_be_positive(long minorUnits)
    {
        var act = () => Monthly(InAugust, minorUnits);

        act.Should().Throw<InvalidBudgetException>().WithMessage("*must be positive*");
    }

    /// <summary>
    /// The dates are dates. A budget starts on a day, so any time of day handed in is dropped rather
    /// than kept to surprise a later comparison.
    /// </summary>
    [Test]
    public void A_budget_keeps_only_the_date_part_of_its_window()
    {
        var budget = Monthly(startsOn: new DateTime(2026, 8, 15, 17, 45, 12, DateTimeKind.Utc));

        budget.StartsOn.Should().Be(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));
        budget.StartsOn.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void Restating_a_budget_applies_the_same_rules_as_creating_one()
    {
        var budget = Monthly(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        var act = () => budget.Restate(Money.OfMinorUnits(500), Recurrence.None, InAugust, endsOn: null);

        act.Should().Throw<InvalidBudgetException>().WithMessage("*must state when it ends*");
        budget.AmountMinorUnits.Should().Be(30000, "a rejected restatement must not half-apply");
    }

    [Test]
    public void Restating_a_budget_replaces_its_limit_and_window_and_touches_it()
    {
        var budget = Monthly(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        budget.Restate(
            Money.OfMinorUnits(50000, Currency.USD),
            Recurrence.Yearly,
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endsOn: null);

        budget.AmountMinorUnits.Should().Be(50000);
        budget.Currency.Should().Be(Currency.USD);
        budget.Recurrence.Should().Be(Recurrence.Yearly);
        budget.StartsOn.Should().Be(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        budget.UpdatedAt.Should().NotBeNull();
    }

    private static Entities.Budget Monthly(DateTime startsOn, long minorUnits = 30000) =>
        Entities.Budget.For(
            Category(), Money.OfMinorUnits(minorUnits), Recurrence.Monthly, startsOn, endsOn: null);

    private static Entities.Budget Weekly(DateTime startsOn) =>
        Entities.Budget.For(
            Category(), Money.OfMinorUnits(10000), Recurrence.Weekly, startsOn, endsOn: null);

    private static Entities.Budget Yearly(DateTime startsOn) =>
        Entities.Budget.For(
            Category(), Money.OfMinorUnits(500000), Recurrence.Yearly, startsOn, endsOn: null);

    private static Entities.Category Category() => new()
    {
        Label = "Food",
        Priority = new Entities.Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow },
        CreatedAt = DateTime.UtcNow
    };
}
