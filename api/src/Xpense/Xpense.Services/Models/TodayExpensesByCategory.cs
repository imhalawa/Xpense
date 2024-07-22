using Xpense.Services.ValueObjects;

namespace Xpense.Services.Models
{
    public class TodayExpensesByCategory(IEnumerable<ExpensesByCategory>? expenses, Money total)
    {
        public IEnumerable<ExpensesByCategory>? Expenses { get; set; } = expenses;
        public Money Total { get; set; } = total;
    }
}
