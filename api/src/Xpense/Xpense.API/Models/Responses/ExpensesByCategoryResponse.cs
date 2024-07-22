using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.API.Models.Responses
{
    public class ExpensesByCategoryResponse(int id, CategoryResponse category, Money amount)
    {
        public int Id { get; set; } = id;
        public CategoryResponse Category { get; set; } = category;
        public Money Amount { get; set; } = amount;

        public static ExpensesByCategoryResponse Of(ExpensesByCategory expensesByCategory) => new ExpensesByCategoryResponse(expensesByCategory.Id, CategoryResponse.Of(expensesByCategory.Category), expensesByCategory.Amount);
    }
}
