using Microsoft.EntityFrameworkCore;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Events;
using Xpense.Domain.ValueObjects;
using Xpense.Persistence;

namespace Xpense.Notifications.Rules;

public sealed class BudgetExceededRule(XpenseDbContext dbContext) : INotificationRule<TransactionRecorded>
{
    public async Task<IReadOnlyList<NotificationDraft>> Evaluate(
        Event<TransactionRecorded> @event,
        CancellationToken cancellationToken)
    {
        var body = @event.Body;

        if (body.Kind != TransactionKind.Expense || body.CategoryId is null)
            return [];

        var budgets = await dbContext.Budgets
            .AsNoTracking()
            .Include(budget => budget.Category)
            .Where(budget => budget.CategoryId == body.CategoryId && budget.Currency == body.Currency)
            .ToListAsync(cancellationToken);

        if (budgets.Count == 0)
            return [];

        var drafts = new List<NotificationDraft>();

        foreach (var budget in budgets)
        {
            var period = budget.PeriodOn(body.OccurredAt);

            if (period is null)
                continue;

            var after = await SpentIn(budget, period, cancellationToken);
            var before = after - Money.OfMinorUnits(body.AmountMinorUnits, budget.Currency);

            if (before > budget.Amount || after <= budget.Amount)
                continue;

            drafts.Add(Draft(budget, period.Name, after));
        }

        return drafts;
    }

    private async Task<Money> SpentIn(Budget budget, BudgetPeriod period, CancellationToken cancellationToken)
    {
        var minorUnits = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.SourceAccountId != null
                                  && transaction.DestinationAccountId == null
                                  && transaction.CategoryId == budget.CategoryId
                                  && transaction.Currency == budget.Currency
                                  && transaction.OccurredAt >= period.From
                                  && transaction.OccurredAt < period.ToExclusive)
            .SumAsync(transaction => transaction.AmountMinorUnits, cancellationToken);

        return Money.OfMinorUnits(minorUnits, budget.Currency);
    }

    private static NotificationDraft Draft(Budget budget, string period, Money spent)
    {
        var over = spent - budget.Amount;
        var label = budget.Category!.Label;

        return new NotificationDraft(
            NotificationKind.BudgetExceeded,
            $"{label} is over budget",
            $"You have spent {spent} of your {budget.Amount} budget for {label} in {period}, "
            + $"which is {over} over.",
            new BudgetExceededPayload(
                budget.Id,
                budget.CategoryId,
                label,
                period,
                budget.AmountMinorUnits,
                spent.MinorUnits,
                over.MinorUnits,
                budget.Currency.ToString()));
    }

    private sealed record BudgetExceededPayload(
        int BudgetId,
        int CategoryId,
        string CategoryLabel,
        string Period,
        long LimitMinorUnits,
        long SpentMinorUnits,
        long ExceededByMinorUnits,
        string Currency);
}
