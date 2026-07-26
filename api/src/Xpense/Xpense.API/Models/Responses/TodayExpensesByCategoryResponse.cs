using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Models;

namespace Xpense.API.Models.Responses
{
    public class TodayExpensesByCategoryResponse(IEnumerable<ExpensesByCategoryResponse> expenses, V1AnalyticsMoneyResponse total)
    {
        public IEnumerable<ExpensesByCategoryResponse> Expenses { get; set; } = expenses;
        public V1AnalyticsMoneyResponse Total { get; set; } = total;

        public static TodayExpensesByCategoryResponse Of(TodayExpensesByCategory expensesByCategory)
        {
            var expenses = expensesByCategory?
                .Expenses?
                .Select(ExpensesByCategoryResponse.Of);
            var total = V1AnalyticsMoneyResponse.Of(expensesByCategory?.Total);

            return new TodayExpensesByCategoryResponse(expenses, total);
        }
    }

    public sealed record V1AnalyticsMoneyResponse(long Cents, string Currency)
    {
        public static V1AnalyticsMoneyResponse Of(Xpense.Services.ValueObjects.Money money)
        {
            return new V1AnalyticsMoneyResponse(money?.Cents ?? 0, money?.Currency.ToString() ?? "EUR");
        }
    }
}
