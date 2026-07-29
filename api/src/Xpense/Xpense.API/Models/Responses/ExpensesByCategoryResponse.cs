using Xpense.Services.Models;

namespace Xpense.API.Models.Responses
{
    public class ExpensesByCategoryResponse(int id, CategoryResponse category, AnalyticsMoneyResponse amount)
    {
        public int Id { get; set; } = id;
        public CategoryResponse Category { get; set; } = category;
        public AnalyticsMoneyResponse Amount { get; set; } = amount;

        public static ExpensesByCategoryResponse Of(ExpensesByCategory expensesByCategory) => new ExpensesByCategoryResponse(expensesByCategory.Id, CategoryResponse.Of(expensesByCategory.Category), AnalyticsMoneyResponse.Of(expensesByCategory.Amount));
    }
}
