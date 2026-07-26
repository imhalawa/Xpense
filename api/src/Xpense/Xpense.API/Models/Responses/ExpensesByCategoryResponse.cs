using Xpense.Services.Models;

namespace Xpense.API.Models.Responses
{
    public class ExpensesByCategoryResponse(int id, CategoryResponse category, V1AnalyticsMoneyResponse amount)
    {
        public int Id { get; set; } = id;
        public CategoryResponse Category { get; set; } = category;
        public V1AnalyticsMoneyResponse Amount { get; set; } = amount;

        public static ExpensesByCategoryResponse Of(ExpensesByCategory expensesByCategory) => new ExpensesByCategoryResponse(expensesByCategory.Id, CategoryResponse.Of(expensesByCategory.Category), V1AnalyticsMoneyResponse.Of(expensesByCategory.Amount));
    }
}
