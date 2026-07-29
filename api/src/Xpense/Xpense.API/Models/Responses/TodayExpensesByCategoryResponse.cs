using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Models;

namespace Xpense.API.Models.Responses
{
    public class TodayExpensesByCategoryResponse(IEnumerable<ExpensesByCategoryResponse> expenses, AnalyticsMoneyResponse total)
    {
        public IEnumerable<ExpensesByCategoryResponse> Expenses { get; set; } = expenses;
        public AnalyticsMoneyResponse Total { get; set; } = total;

        public static TodayExpensesByCategoryResponse Of(TodayExpensesByCategory expensesByCategory)
        {
            var expenses = expensesByCategory?
                .Expenses?
                .Select(ExpensesByCategoryResponse.Of);
            var total = AnalyticsMoneyResponse.Of(expensesByCategory?.Total);

            return new TodayExpensesByCategoryResponse(expenses, total);
        }
    }

    public sealed record AnalyticsMoneyResponse(long Cents, string Currency)
    {
        public static AnalyticsMoneyResponse Of(Xpense.Services.ValueObjects.Money money)
        {
            return new AnalyticsMoneyResponse(money?.Cents ?? 0, money?.Currency.ToString() ?? "EUR");
        }
    }
}
