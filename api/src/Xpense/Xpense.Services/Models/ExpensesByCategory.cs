using Xpense.Services.Entities;
using Xpense.Services.ValueObjects;

namespace Xpense.Services.Models
{
    public class ExpensesByCategory(int id, Category category, Money amount)
    {
        public int Id { get; set; } = id;
        public Category Category { get; set; } = category;
        public Money Amount { get; set; } = amount;
    }
}
