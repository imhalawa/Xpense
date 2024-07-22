using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.API.Models.Responses
{
    public class TodayExpensesByCategoryResponse(IEnumerable<ExpensesByCategoryResponse> expenses, Money total)
    {
        public IEnumerable<ExpensesByCategoryResponse> Expenses { get; set; } = expenses;
        public Money Total { get; set; } = total;

        public static TodayExpensesByCategoryResponse Of(TodayExpensesByCategory expensesByCategory)
        {
            var expenses = expensesByCategory?
                .Expenses?
                .Select(ExpensesByCategoryResponse.Of);
            var total = expensesByCategory?.Total ?? Money.Zero;

            return new TodayExpensesByCategoryResponse(expenses, total);
        }
    }
}
